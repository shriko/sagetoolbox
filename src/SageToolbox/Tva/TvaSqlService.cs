using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Tva;

/// <summary>Lecture seule : liste les bases Sage Comptabilité de l'instance connectée et exécute les contrôles TVA
/// (authentification Windows intégrée, comme le reste de la Toolbox).</summary>
public sealed class TvaSqlService(string instance)
{
    private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";

    /// <summary>
    /// Liste les bases en ligne contenant une comptabilité Sage (P_DOSSIER + F_ECRITUREC),
    /// avec société, taille et nombre d'écritures des années N et N-1.
    /// </summary>
    public async Task<List<BaseInfo>> ListBasesAsync(CancellationToken ct)
    {
        await using var master = DatabaseManager.Instance.CreateConnection(instance, "master");
        await master.OpenAsync(ct);

        var names = new List<string>();
        await using (var cmd = new SqlCommand(
            "SELECT name FROM sys.databases WHERE state = 0 AND database_id > 4 ORDER BY name", master))
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
        {
            while (await rd.ReadAsync(ct))
                names.Add(rd.GetString(0));
        }

        var annee = DateTime.Now.Year;
        var bases = new List<BaseInfo>();

        foreach (var name in names)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var q = Quote(name);
                var sql = $"""
                    IF OBJECT_ID(N'{q.Replace("'", "''")}.dbo.P_DOSSIER') IS NULL
                       OR OBJECT_ID(N'{q.Replace("'", "''")}.dbo.F_ECRITUREC') IS NULL
                        SELECT CAST(NULL AS nvarchar(100)), 0, 0, 0
                    ELSE
                        SELECT (SELECT TOP 1 D_RaisonSoc FROM {q}.dbo.P_DOSSIER),
                               (SELECT COUNT(*) FROM {q}.dbo.F_ECRITUREC WHERE JM_Date >= @n  AND JM_Date < @n1),
                               (SELECT COUNT(*) FROM {q}.dbo.F_ECRITUREC WHERE JM_Date >= @n0 AND JM_Date < @n),
                               (SELECT CAST(SUM(size) * 8.0 / 1024 AS decimal(12,2)) FROM sys.master_files WHERE database_id = DB_ID(@db))
                    """;
                await using var cmd = new SqlCommand(sql, master);
                cmd.Parameters.AddWithValue("@db", name);
                cmd.Parameters.AddWithValue("@n0", new DateTime(annee - 1, 1, 1));
                cmd.Parameters.AddWithValue("@n", new DateTime(annee, 1, 1));
                cmd.Parameters.AddWithValue("@n1", new DateTime(annee + 1, 1, 1));
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                if (!await rd.ReadAsync(ct) || rd.IsDBNull(0)) continue;

                bases.Add(new BaseInfo
                {
                    Nom = name,
                    Societe = rd.GetString(0),
                    NbEcrituresN = rd.GetInt32(1),
                    NbEcrituresN1 = rd.GetInt32(2),
                    TailleMo = rd.IsDBNull(3) ? 0 : rd.GetDecimal(3)
                });
            }
            catch (SqlException)
            {
                // Base inaccessible (droits insuffisants) ou structure inattendue : ignorée.
            }
        }

        return bases;
    }

    /// <summary>Exécute tous les contrôles sur une base ; une erreur sur un contrôle n'arrête pas les autres.</summary>
    public async Task<BaseAnalysis> AnalyseAsync(BaseInfo info, IProgress<string> log, CancellationToken ct)
    {
        var analysis = new BaseAnalysis { Info = info };

        await using var conn = DatabaseManager.Instance.CreateConnection(instance, info.Nom);
        await conn.OpenAsync(ct);

        foreach (var def in AuditControls.All)
        {
            ct.ThrowIfCancellationRequested();
            var result = new ControlResult { Definition = def };
            try
            {
                await using var cmd = new SqlCommand(def.Sql, conn) { CommandTimeout = 120 };
                await using var rd = await cmd.ExecuteReaderAsync(ct);

                var columns = Enumerable.Range(0, rd.FieldCount).Select(rd.GetName).ToArray();
                var rows = new List<string[]>();
                while (await rd.ReadAsync(ct))
                {
                    var row = new string[columns.Length];
                    for (var i = 0; i < row.Length; i++)
                        row[i] = rd.IsDBNull(i) ? "" : Convert.ToString(rd.GetValue(i), System.Globalization.CultureInfo.CurrentCulture)!.Trim();
                    rows.Add(row);
                }

                Fill(result, columns, rows);
                log.Report($"   {def.Id} {def.Title} : {result.Rows.Count} ligne(s), {result.AnomalyCount} anomalie(s)");
            }
            catch (SqlException ex)
            {
                result.Error = ex.Message;
                log.Report($"   {def.Id} {def.Title} : ERREUR - {ex.Message}");
            }
            analysis.Results.Add(result);
        }

        return analysis;
    }

    private static void Fill(ControlResult result, string[] columns, List<string[]> rows)
    {
        var def = result.Definition;

        if (def.Transposed && rows.Count == 1)
        {
            result.Columns = ["Paramètre", "Valeur"];
            for (var i = 0; i < columns.Length; i++)
                result.Rows.Add([columns[i], rows[0][i]]);
            return;
        }

        result.Columns = columns;
        result.Rows.AddRange(rows);

        if (def.AnomalyColumn is null) return;
        var idx = Array.IndexOf(columns, def.AnomalyColumn);
        if (idx < 0) return;

        for (var i = 0; i < rows.Count; i++)
        {
            var v = rows[i][idx];
            if (v.Length > 0 && !v.Equals("Non", StringComparison.OrdinalIgnoreCase))
                result.AnomalyRows.Add(i);
        }
    }
}
