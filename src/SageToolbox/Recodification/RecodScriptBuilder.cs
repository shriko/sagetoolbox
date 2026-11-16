using System.Text;
using System.Text.RegularExpressions;

namespace SageToolbox.Recodification;

/// <summary>Un UPDATE issu d'un script de recodification (corps du curseur).</summary>
public sealed record TemplateStatement(string Condition, string Update, string Table, IReadOnlyList<string> SetColumns)
{
    public string Label => $"{Table}.{string.Join(", ", SetColumns)}";
}

public sealed record SkippedStatement(TemplateStatement Statement, string Reason);

/// <summary>Informations nécessaires pour générer la variante « fusion » : fiche maître et index uniques de la base.</summary>
public sealed record MergeContext(
    string MasterTable,
    string KeyColumn,
    string? PlanColumn,
    IReadOnlyDictionary<string, IReadOnlyList<string[]>> UniqueIndexes);

public sealed record RecodScript(
    string Sql,
    IReadOnlyList<TemplateStatement> Included,
    IReadOnlyList<SkippedStatement> Skipped);

/// <summary>
/// Transforme un script modèle (liste d'UPDATE utilisant @ancien, @nouveau, @plan et @m_cial/@m_immo/@m_tres)
/// en un lot SQL complet : désactivation des déclencheurs, curseur sur #mapping, journalisation des lignes modifiées.
/// Les UPDATE visant une table ou une colonne absente de la base (module non installé, autre version) sont écartés.
/// Avec un <see cref="MergeContext"/>, les lignes de #mapping marquées merge = 1 sont fusionnées dans une fiche existante.
/// </summary>
public static partial class RecodScriptBuilder
{
    public static IReadOnlyList<TemplateStatement> Parse(string template)
    {
        var result = new List<TemplateStatement>();
        foreach (var chunk in StatementSplit().Split(template))
        {
            var text = chunk.Trim();
            if (text.Length == 0) continue;
            var m = StatementShape().Match(text);
            if (!m.Success) throw new FormatException($"Instruction inattendue dans le modèle : {text}");

            var update = m.Groups["upd"].Value.Trim();
            var table = TableName().Match(update).Groups[1].Value;
            var setPart = SetPart().Match(update).Groups[1].Value;
            var cols = SetColumn().Matches(setPart).Select(c => c.Groups[1].Value).ToList();
            result.Add(new TemplateStatement(m.Groups["cond"].Value.Trim(), update, table, cols));
        }
        return result;
    }

    /// <param name="sqlSchema">Schéma SQL qui préfixe les tables Sage du lot généré (dbo sur SQL Server, schéma de la société en SPC) ;
    /// null laisse les noms de table tels quels.</param>
    public static RecodScript Build(string template, IReadOnlyDictionary<string, HashSet<string>> schema, MergeContext? merge = null, string? sqlSchema = null)
    {
        var included = new List<TemplateStatement>();
        var skipped = new List<SkippedStatement>();
        foreach (var st in Parse(template))
        {
            var reason = CheckAgainstSchema(st, schema);
            if (reason is null) included.Add(st); else skipped.Add(new SkippedStatement(st, reason));
        }

        var tables = included.Select(s => s.Table).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("DECLARE @m_cial bit = CASE WHEN EXISTS(SELECT 1 FROM cbSysTable WHERE CB_Type = 'CIAL') THEN 1 ELSE 0 END;");
        sb.AppendLine("DECLARE @m_immo bit = CASE WHEN EXISTS(SELECT 1 FROM cbSysTable WHERE CB_Type = 'IMMO') THEN 1 ELSE 0 END;");
        sb.AppendLine("DECLARE @m_tres bit = CASE WHEN EXISTS(SELECT 1 FROM cbSysTable WHERE CB_Type = 'TRES') THEN 1 ELSE 0 END;");
        sb.AppendLine("DECLARE @ancien varchar(35), @nouveau varchar(35), @plan int, @merge bit, @r int;");
        foreach (var t in tables)
        {
            sb.AppendLine($"ALTER TABLE [{t}] DISABLE TRIGGER ALL;");
            sb.AppendLine($"ALTER TABLE [{t}] NOCHECK CONSTRAINT ALL;");
        }
        sb.AppendLine("DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT ancien, nouveau, nplan, fusion FROM #mapping ORDER BY id;");
        sb.AppendLine("OPEN c;");
        sb.AppendLine("FETCH NEXT FROM c INTO @ancien, @nouveau, @plan, @merge;");
        sb.AppendLine("WHILE @@FETCH_STATUS = 0");
        sb.AppendLine("BEGIN");
        for (var i = 0; i < included.Count; i++)
            AppendStatement(sb, included[i], i, schema, merge);
        sb.AppendLine("FETCH NEXT FROM c INTO @ancien, @nouveau, @plan, @merge;");
        sb.AppendLine("END");
        sb.AppendLine("CLOSE c; DEALLOCATE c;");
        foreach (var t in tables)
        {
            sb.AppendLine($"ALTER TABLE [{t}] ENABLE TRIGGER ALL;");
            sb.AppendLine($"ALTER TABLE [{t}] CHECK CONSTRAINT ALL;");
        }
        var sql = sb.ToString();
        if (sqlSchema is not null) sql = QualifyTables(sql, sqlSchema);
        return new RecodScript(sql, included, skipped);
    }

    /// <summary>Préfixe du schéma les tables Sage (F_…, P_…, cbSysTable) nommées après UPDATE / FROM / JOIN / TABLE ; tables temporaires, curseur et alias sont ignorés.</summary>
    internal static string QualifyTables(string sql, string sqlSchema)
    {
        var prefix = "[" + sqlSchema.Replace("]", "]]") + "].";
        return TableReference().Replace(sql, m => prefix + "[" + m.Groups["t"].Value + "]");
    }

    [GeneratedRegex(@"(?<=\b(?:UPDATE|FROM|JOIN|TABLE)\s+)\[?(?<t>(?:F|P)_\w+|cbSysTable)\]?", RegexOptions.IgnoreCase)]
    private static partial Regex TableReference();

    // #log : idx >= 0 = lignes mises à jour par l'instruction idx ; idx < 0 = lignes supprimées (fusion) par l'instruction -1 - idx.
    private static void AppendStatement(StringBuilder sb, TemplateStatement st, int i,
        IReadOnlyDictionary<string, HashSet<string>> schema, MergeContext? merge)
    {
        var prefix = st.Condition.Length > 0 ? "IF " + st.Condition + " " : "";

        void Plain()
        {
            sb.AppendLine($"{prefix}BEGIN");
            sb.AppendLine(st.Update + ";");
            sb.AppendLine($"SET @r = @@ROWCOUNT; IF @r > 0 INSERT #log(idx, n) VALUES ({i}, @r);");
            sb.AppendLine("END");
        }

        if (merge is null) { Plain(); return; }

        if (st.Table.Equals(merge.MasterTable, StringComparison.OrdinalIgnoreCase)
            && st.SetColumns.Contains(merge.KeyColumn, StringComparer.OrdinalIgnoreCase))
        {
            // Fiche maître : en fusion on reporte les autres références (ex. payeur) puis on supprime la fiche source ;
            // la fiche de destination n'est pas modifiée.
            sb.AppendLine("IF @merge = 1 BEGIN");
            foreach (var col in st.SetColumns.Where(c => !c.Equals(merge.KeyColumn, StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine($"UPDATE {st.Table} SET {col} = @nouveau WHERE {col} = @ancien;");
                sb.AppendLine($"SET @r = @@ROWCOUNT; IF @r > 0 INSERT #log(idx, n) VALUES ({i}, @r);");
            }
            var plan = merge.PlanColumn is null ? "" : $" AND {merge.PlanColumn} = @plan";
            sb.AppendLine($"DELETE FROM {st.Table} WHERE {merge.KeyColumn} = @ancien{plan};");
            sb.AppendLine($"SET @r = @@ROWCOUNT; IF @r > 0 INSERT #log(idx, n) VALUES ({-1 - i}, @r);");
            sb.AppendLine("END ELSE BEGIN");
            sb.AppendLine(st.Update + ";");
            sb.AppendLine($"SET @r = @@ROWCOUNT; IF @r > 0 INSERT #log(idx, n) VALUES ({i}, @r);");
            sb.AppendLine("END");
            return;
        }

        var delete = st.SetColumns.Count == 1 && schema.TryGetValue(st.Table, out var cols)
            ? CollisionDelete(st, merge, cols)
            : null;
        if (delete is not null)
        {
            // Fusion : la ligne liée de la fiche source qui ferait doublon avec une ligne de la destination est supprimée (la destination prime).
            sb.AppendLine($"IF @merge = 1{(st.Condition.Length > 0 ? " AND " + st.Condition : "")} BEGIN");
            sb.AppendLine(delete + ";");
            sb.AppendLine($"SET @r = @@ROWCOUNT; IF @r > 0 INSERT #log(idx, n) VALUES ({-1 - i}, @r);");
            sb.AppendLine("END");
        }
        Plain();
    }

    /// <summary>
    /// DELETE des lignes liées à l'ancien code qui entreraient en conflit avec un index unique une fois reportées sur le nouveau code.
    /// Les colonnes miroir Sage (cbXxx) des index sont ramenées à la colonne réelle.
    /// </summary>
    private static string? CollisionDelete(TemplateStatement st, MergeContext merge, HashSet<string> tableColumns)
    {
        if (!merge.UniqueIndexes.TryGetValue(st.Table, out var indexes)) return null;
        var col = st.SetColumns[0];

        var others = new List<string[]>();
        foreach (var index in indexes)
        {
            var mapped = index.Select(c => c.StartsWith("cb", StringComparison.OrdinalIgnoreCase) && tableColumns.Contains(c[2..]) ? c[2..] : c).ToArray();
            if (!mapped.Contains(col, StringComparer.OrdinalIgnoreCase)) continue;
            var rest = mapped.Where(c => !c.Equals(col, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (rest.Any(c => !tableColumns.Contains(c))) continue;
            if (!others.Any(o => o.SequenceEqual(rest, StringComparer.OrdinalIgnoreCase))) others.Add(rest);
        }
        if (others.Count == 0) return null;

        var where = WhereClause().Match(st.Update);
        if (!where.Success) return null;

        var exists = others.Select(rest =>
            $"EXISTS (SELECT 1 FROM {st.Table} d WHERE d.{col} = @nouveau{string.Concat(rest.Select(c => $" AND d.{c} = s.{c}"))})");
        return $"DELETE s FROM {st.Table} AS s WHERE ({where.Groups[1].Value.Trim()}) AND ({string.Join(" OR ", exists)})";
    }

    private static string? CheckAgainstSchema(TemplateStatement st, IReadOnlyDictionary<string, HashSet<string>> schema)
    {
        if (!schema.TryGetValue(st.Table, out var columns))
            return $"table {st.Table} absente";

        // Sous-requêtes EXISTS(SELECT 1 FROM T WHERE col ...) : table et colonne vérifiées séparément.
        var main = st.Update;
        foreach (Match sub in Exists().Matches(st.Update))
        {
            var t = sub.Groups[1].Value;
            if (!schema.TryGetValue(t, out var subCols)) return $"table {t} absente";
            foreach (Match id in Identifier().Matches(sub.Groups[2].Value))
                if (!subCols.Contains(id.Value)) return $"colonne {t}.{id.Value} absente";
            main = main.Replace(sub.Value, " ");
        }

        foreach (Match id in Identifier().Matches(main))
        {
            if (id.Value.Equals(st.Table, StringComparison.OrdinalIgnoreCase)) continue;
            if (!columns.Contains(id.Value)) return $"colonne {st.Table}.{id.Value} absente";
        }
        return null;
    }

    [GeneratedRegex(@"(?im)^(?=\s*(?:IF\s+@m_\w+\s*=\s*1\s+)?UPDATE\b)")]
    private static partial Regex StatementSplit();

    [GeneratedRegex(@"^(?:IF\s+(?<cond>@m_\w+\s*=\s*1)\s+)?(?<upd>UPDATE\b[\s\S]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex StatementShape();

    [GeneratedRegex(@"^UPDATE\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex TableName();

    [GeneratedRegex(@"\bSET\b([\s\S]*?)\bWHERE\b", RegexOptions.IgnoreCase)]
    private static partial Regex SetPart();

    [GeneratedRegex(@"\bWHERE\b([\s\S]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex WhereClause();

    [GeneratedRegex(@"(?:^|,)\s*(\w+)\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex SetColumn();

    [GeneratedRegex(@"EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+(\w+)\s+WHERE\s+([^)]*)\)", RegexOptions.IgnoreCase)]
    private static partial Regex Exists();

    // Colonnes Sage : préfixe_Nom (au moins un « _ »), hors variables @… et hors noms de table.
    [GeneratedRegex(@"(?<![@\w])[A-Za-z][A-Za-z0-9]*_[A-Za-z0-9_]+\b")]
    private static partial Regex Identifier();
}
