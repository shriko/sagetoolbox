using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Recodification;

public sealed record StatementResult(string Label, int Rows);

public sealed record RecodResult(
    bool Success,
    string? Error,
    DateTime Start,
    DateTime End,
    int MappingCount,
    IReadOnlyList<StatementResult> Updated,
    IReadOnlyList<SkippedStatement> Skipped,
    int ExecutedStatements,
    int Merged = 0);

public static class RecodExecutor
{
    public static string LoadTemplate(string name)
    {
        var asm = typeof(RecodExecutor).Assembly;
        using var s = asm.GetManifestResourceStream($"Recodification.Sql.{name}.sql")
                      ?? throw new InvalidOperationException($"Script de recodification introuvable : {name}");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>
    /// Applique les recodifications dans UNE transaction : en cas d'erreur ou d'écart de contrôle, tout est annulé
    /// (y compris la désactivation des déclencheurs).
    /// </summary>
    public static async Task<RecodResult> RunAsync(RecodRepository db, ElementDefinition def, IReadOnlyList<RecodItem> changes, bool commit = true)
    {
        var start = DateTime.Now;
        var schema = await db.LoadSchemaAsync();
        var merge = def.SupportsMerge && changes.Any(c => c.IsMerge)
            ? new MergeContext(def.Table, def.KeyColumn, def.PlanColumn, await db.LoadUniqueIndexesAsync())
            : null;
        var script = RecodScriptBuilder.Build(LoadTemplate(def.TemplateName), schema, merge, DatabaseManager.Instance.SchemaName);

        try
        {
            await using var conn = DatabaseManager.Instance.CreateConnection();
            await conn.OpenAsync();

            await Exec(conn, null, "CREATE TABLE #mapping (id int IDENTITY PRIMARY KEY, ancien varchar(35) COLLATE DATABASE_DEFAULT NOT NULL, nouveau varchar(35) COLLATE DATABASE_DEFAULT NOT NULL, nplan int NOT NULL, fusion bit NOT NULL); " +
                                   "CREATE TABLE #log (idx int NOT NULL, n int NOT NULL);");

            var table = new DataTable();
            table.Columns.Add("ancien", typeof(string));
            table.Columns.Add("nouveau", typeof(string));
            table.Columns.Add("nplan", typeof(int));
            table.Columns.Add("fusion", typeof(bool));
            foreach (var i in changes) table.Rows.Add(i.Code, i.EffectiveNewCode, i.Plan, i.IsMerge);

            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tx) { DestinationTableName = "#mapping" })
                {
                    bulk.ColumnMappings.Add("ancien", "ancien");
                    bulk.ColumnMappings.Add("nouveau", "nouveau");
                    bulk.ColumnMappings.Add("nplan", "nplan");
                    bulk.ColumnMappings.Add("fusion", "fusion");
                    await bulk.WriteToServerAsync(table);
                }

                await Exec(conn, tx, script.Sql);

                var updated = new List<StatementResult>();
                await using (var cmd = new SqlCommand("SELECT idx, SUM(n) FROM #log GROUP BY idx ORDER BY CASE WHEN idx < 0 THEN -1 - idx ELSE idx END, idx DESC", conn, tx))
                await using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                    {
                        var idx = r.GetInt32(0);
                        var st = script.Included[idx < 0 ? -1 - idx : idx];
                        updated.Add(new StatementResult(
                            idx >= 0 ? st.Label
                            : st.Table.Equals(def.Table, StringComparison.OrdinalIgnoreCase) ? $"{st.Table} : fiches sources supprimées (fusion)"
                            : $"{st.Table} : doublons supprimés (la destination prime)",
                            r.GetInt32(1)));
                    }

                await VerifyAsync(conn, tx, def, changes.Count);
                if (commit) await tx.CommitAsync(); else await tx.RollbackAsync();

                return new RecodResult(true, null, start, DateTime.Now, changes.Count, updated, script.Skipped, script.Included.Count,
                    changes.Count(c => c.IsMerge));
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new RecodResult(false, ex.Message, start, DateTime.Now, changes.Count, [], script.Skipped, script.Included.Count, changes.Count(c => c.IsMerge));
        }
    }

    // Après traitement : plus aucun ancien code dans la table, et tous les nouveaux y sont.
    private static async Task VerifyAsync(SqlConnection conn, SqlTransaction tx, ElementDefinition def, int expected)
    {
        var plan = def.PlanColumn is null ? "" : $" AND t.{def.PlanColumn} = m.nplan";
        var sql = $"""
            SELECT
              (SELECT COUNT(*) FROM {RecodRepository.Q(def.Table)} t JOIN #mapping m ON t.{def.KeyColumn} = m.ancien{plan}),
              (SELECT COUNT(*) FROM {RecodRepository.Q(def.Table)} t JOIN #mapping m ON t.{def.KeyColumn} = m.nouveau{plan})
            """;
        await using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 0 };
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        int remaining = r.GetInt32(0), found = r.GetInt32(1);
        if (remaining != 0 || found != expected)
            throw new InvalidOperationException(
                $"Contrôle final en échec : {remaining} ancien(s) code(s) encore présent(s), {found}/{expected} nouveau(x) code(s) trouvé(s). Traitement annulé.");
    }

    private static async Task Exec(SqlConnection conn, SqlTransaction? tx, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 0 };
        await cmd.ExecuteNonQueryAsync();
    }
}
