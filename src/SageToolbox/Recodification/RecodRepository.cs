using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Recodification;

public sealed record SessionInfo(int Id, string Login, string Host, string Program);
public sealed record BackupInfo(DateTime Finished, string Device, DateTime ServerNow);

/// <summary>
/// Accès SQL à la base (ou à la société SPC) connectée pour la recodification : lecture des fiches, des clés, de la structure,
/// contrôles préalables (sessions, sauvegarde). Toutes les tables Sage sont préfixées du schéma courant (dbo ou schéma de la société SPC).
/// </summary>
public sealed class RecodRepository
{
    private static DatabaseManager Db => DatabaseManager.Instance;

    /// <summary>Nom de table qualifié par le schéma courant, prêt à être inséré dans un SQL.</summary>
    public static string Q(string table) => $"{DatabaseManager.QuoteIdentifier(Db.SchemaName)}.{DatabaseManager.QuoteIdentifier(table)}";

    private static async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var c = Db.CreateConnection();
        await c.OpenAsync(ct);
        return c;
    }

    private static string Str(SqlDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i).Trim();

    public async Task<bool> HasGescomAsync()
    {
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(@a) IS NOT NULL AND OBJECT_ID(@b) IS NOT NULL THEN 1 ELSE 0 END", c);
        cmd.Parameters.AddWithValue("@a", $"{Db.SchemaName}.F_ARTICLE");
        cmd.Parameters.AddWithValue("@b", $"{Db.SchemaName}.F_FAMILLE");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    public async Task<List<RecodItem>> LoadItemsAsync(ElementDefinition def)
    {
        var plan = def.PlanColumn ?? "0";
        var where = def.Filter is null ? "" : $" WHERE {def.Filter}";
        var sql = $"SELECT {plan}, {def.KeyColumn}, {def.LabelColumn} FROM {Q(def.Table)}{where} ORDER BY {(def.HasPlan ? plan + ", " : "")}{def.KeyColumn}";
        var items = new List<RecodItem>();
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 0 };
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            items.Add(new RecodItem(Convert.ToInt32(r.GetValue(0)), Str(r, 1), Str(r, 2)));
        return items;
    }

    /// <summary>Toutes les clés de la table (sans filtre de type) : un nouveau code ne doit en reprendre aucune.</summary>
    public async Task<List<(int Plan, string Code, int Nature)>> LoadAllKeysAsync(ElementDefinition def)
    {
        var plan = def.PlanColumn ?? "0";
        var nature = def.NatureColumn ?? "0";
        var keys = new List<(int, string, int)>();
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand($"SELECT {plan}, {def.KeyColumn}, {nature} FROM {Q(def.Table)}", c) { CommandTimeout = 0 };
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) keys.Add((Convert.ToInt32(r.GetValue(0)), Str(r, 1), Convert.ToInt32(r.GetValue(2))));
        return keys;
    }

    /// <summary>Index uniques (hors clé primaire) par table : colonnes de clé dans l'ordre de l'index.</summary>
    public async Task<Dictionary<string, IReadOnlyList<string[]>>> LoadUniqueIndexesAsync()
    {
        const string sql = """
            SELECT t.name, i.index_id, c.name
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.is_unique = 1 AND i.is_primary_key = 0 AND i.has_filter = 0 AND t.schema_id = SCHEMA_ID(@schema)
            ORDER BY t.name, i.index_id, ic.key_ordinal
            """;
        var raw = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);
        var lastKey = (Table: "", Index: -1);
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@schema", Db.SchemaName);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var table = r.GetString(0);
            var index = r.GetInt32(1);
            if (!raw.TryGetValue(table, out var list)) raw[table] = list = [];
            if (lastKey != (table, index)) { list.Add([]); lastKey = (table, index); }
            list[^1].Add(r.GetString(2));
        }
        return raw.ToDictionary(k => k.Key, k => (IReadOnlyList<string[]>)k.Value.Select(x => x.ToArray()).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<int> GetKeyMaxLengthAsync(ElementDefinition def)
    {
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t AND COLUMN_NAME = @c", c);
        cmd.Parameters.AddWithValue("@s", Db.SchemaName);
        cmd.Parameters.AddWithValue("@t", def.Table);
        cmd.Parameters.AddWithValue("@c", def.KeyColumn);
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? 0 : Convert.ToInt32(v);
    }

    /// <summary>Longueur imposée des comptes généraux de détail (P_DOSSIER.D_LgCg), 0 si libre.</summary>
    public async Task<int> GetFixedAccountLengthAsync()
    {
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand($"SELECT TOP 1 D_LgCg FROM {Q("P_DOSSIER")}", c);
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? 0 : Convert.ToInt32(v);
    }

    /// <summary>Table → colonnes, pour écarter les UPDATE visant une structure absente de ce dossier.</summary>
    public async Task<Dictionary<string, HashSet<string>>> LoadSchemaAsync()
    {
        var schema = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand("SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @s", c) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@s", Db.SchemaName);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var t = r.GetString(0);
            if (!schema.TryGetValue(t, out var cols))
                schema[t] = cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            cols.Add(r.GetString(1));
        }
        return schema;
    }

    // ---- Contrôles préalables -------------------------------------------------------------

    /// <summary>
    /// Sessions ouvertes sur la base par d'autres applications. CanCheck = false si le compte ne peut pas voir les autres
    /// sessions, ou en SPC : la base Azure est partagée entre sociétés, les sessions ne peuvent pas être attribuées à une société.
    /// </summary>
    public async Task<(bool CanCheck, List<SessionInfo> Sessions)> GetOtherSessionsAsync()
    {
        if (Db.IsSpc) return (false, []);

        await using var c = await OpenAsync();
        await using (var perm = new SqlCommand("SELECT ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE'), 0)", c))
            if (Convert.ToInt32(await perm.ExecuteScalarAsync()) != 1)
                return (false, []);

        const string sql = """
            SELECT s.session_id, ISNULL(s.login_name, ''), ISNULL(s.host_name, ''), ISNULL(s.program_name, '')
            FROM sys.dm_exec_sessions s
            WHERE s.is_user_process = 1 AND s.database_id = DB_ID() AND s.session_id <> @@SPID
              AND ISNULL(s.program_name, '') <> @app
            ORDER BY s.session_id
            """;
        var list = new List<SessionInfo>();
        await using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@app", "Sage Toolbox");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SessionInfo(Convert.ToInt32(r.GetValue(0)), r.GetString(1).Trim(), r.GetString(2).Trim(), r.GetString(3).Trim()));
        return (true, list);
    }

    public async Task<DateTime> GetServerTimeAsync()
    {
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand("SELECT GETDATE()", c);
        return (DateTime)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Dernière sauvegarde complète enregistrée dans msdb ; null si aucune (ou msdb illisible). Sans objet en SPC.</summary>
    public async Task<BackupInfo?> GetLastBackupAsync()
    {
        const string sql = """
            SELECT TOP 1 b.backup_finish_date, m.physical_device_name, GETDATE()
            FROM msdb.dbo.backupset b
            JOIN msdb.dbo.backupmediafamily m ON m.media_set_id = b.media_set_id
            WHERE b.database_name = DB_NAME() AND b.type = 'D'
            ORDER BY b.backup_finish_date DESC
            """;
        try
        {
            await using var c = await OpenAsync();
            await using var cmd = new SqlCommand(sql, c);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            return new BackupInfo(r.GetDateTime(0), r.GetString(1), r.GetDateTime(2));
        }
        catch (SqlException)
        {
            return null;
        }
    }

    public async Task<string> GetDefaultBackupFolderAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            await using var cmd = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(260))", c);
            return await cmd.ExecuteScalarAsync() as string ?? "";
        }
        catch (SqlException)
        {
            return "";
        }
    }

    /// <summary>Sauvegarde COPY_ONLY (la chaîne de sauvegardes du client n'est pas perturbée) puis vérification. Le chemin est local au SERVEUR SQL.</summary>
    public async Task BackupAsync(string serverPath)
    {
        await using var c = await OpenAsync();
        await using var cmd = new SqlCommand(
            $"BACKUP DATABASE {DatabaseManager.QuoteIdentifier(Db.CurrentDatabase!)} TO DISK = @p WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = @p WITH CHECKSUM;", c)
        { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@p", serverPath);
        await cmd.ExecuteNonQueryAsync();
    }
}

public static class PreflightRules
{
    public static readonly TimeSpan MaxBackupAge = TimeSpan.FromHours(24);

    /// <summary>Sauvegarde valide : récente et postérieure au dernier traitement appliqué dans cette session.</summary>
    public static bool IsBackupValid(BackupInfo? backup, DateTime? lastApplyServerTime) =>
        backup is not null
        && backup.ServerNow - backup.Finished <= MaxBackupAge
        && (lastApplyServerTime is null || backup.Finished > lastApplyServerTime);
}
