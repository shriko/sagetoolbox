using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;
using SageToolbox.Core;
using SageToolbox.Models;

namespace SageToolbox.Repositories;

/// <summary>
/// Découverte des bases Sage sur l'instance, infos base, optimisation, triggers non
/// standards, correction d'encodage.
///
/// Détection des modules installés et des bases Sage : portée depuis le source Python
/// V1 original (AIG_Toolbox.py) — table système `cbsystable`, colonne CB_TYPE (valeurs
/// CPTA/CIAL/TRES/IMMO, une ligne par module installé). Une base est "Sage" si elle
/// possède cette table (méthode get_sage_databases de la V1).
/// </summary>
public sealed class ServerRepository
{
    /// <summary>
    /// Informations techniques de l'instance SQL Server (version, édition/architecture,
    /// mode d'authentification, répertoires par défaut, état FILESTREAM) — affichées dans
    /// le bandeau latéral dès la connexion (instance seule ou base précise).
    /// </summary>
    public async Task<SqlInstanceInfo> GetInstanceInfoAsync(string instance)
    {
        await using var conn = DatabaseManager.Instance.CreateConnection(instance, "master");
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(50)) AS ProductVersion,
                CAST(SERVERPROPERTY('ProductLevel') AS NVARCHAR(50)) AS ProductLevel,
                CAST(SERVERPROPERTY('Edition') AS NVARCHAR(100)) AS Edition,
                CAST(SERVERPROPERTY('IsIntegratedSecurityOnly') AS INT) AS IsIntegratedSecurityOnly,
                CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(500)) AS DataPath,
                CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS NVARCHAR(500)) AS LogPath,
                CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(500)) AS BackupPath,
                CAST(SERVERPROPERTY('FilestreamConfiguredLevel') AS INT) AS FilestreamLevel,
                CAST(SERVERPROPERTY('FilestreamShareName') AS NVARCHAR(100)) AS FilestreamShareName";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Impossible de lire les informations de l'instance.");

        return new SqlInstanceInfo
        {
            ProductVersion = reader["ProductVersion"] as string ?? string.Empty,
            ProductLevel = reader["ProductLevel"] as string ?? string.Empty,
            Edition = reader["Edition"] as string ?? string.Empty,
            WindowsAuthOnly = reader["IsIntegratedSecurityOnly"] is int i && i == 1,
            DataPath = reader["DataPath"] as string ?? string.Empty,
            LogPath = reader["LogPath"] as string ?? string.Empty,
            BackupPath = reader["BackupPath"] as string ?? string.Empty,
            FilestreamLevel = reader["FilestreamLevel"] as int? ?? 0,
            FilestreamShareName = reader["FilestreamShareName"] as string
        };
    }

    public async Task<List<string>> DiscoverSageDatabasesAsync(string instance)
    {
        var result = new List<string>();
        await using var conn = DatabaseManager.Instance.CreateConnection(instance, "master");
        await conn.OpenAsync();

        var candidates = new List<string>();
        await using (var cmd = new SqlCommand(
            "SELECT name FROM sys.databases WHERE state = 0 AND database_id > 4 ORDER BY name", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                candidates.Add(reader.GetString(0));
        }

        foreach (var db in candidates)
        {
            if (await ObjectExistsAsync(conn, db, "cbsystable"))
                result.Add(db);
        }

        return result;
    }

    private static async Task<bool> ObjectExistsAsync(SqlConnection conn, string database, string tableName, SqlTransaction? tx = null)
    {
        await using var cmd = new SqlCommand(
            "SELECT OBJECT_ID(QUOTENAME(@db) + '.dbo.' + @t)", conn, tx);
        cmd.Parameters.AddWithValue("@db", database);
        cmd.Parameters.AddWithValue("@t", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return result is not null && result is not DBNull;
    }

    public async Task<SageDatabaseInfo> GetDatabaseInfoAsync(string instance, string database)
    {
        await using var conn = DatabaseManager.Instance.CreateConnection(instance, database);
        await conn.OpenAsync();

        var installedModules = await GetInstalledModulesAsync(conn);

        double dataMb = 0, logMb = 0, fsMb = 0;
        var physicalPath = string.Empty;

        await using (var cmd = new SqlCommand(
            "SELECT type_desc, CAST(size AS FLOAT) * 8.0 / 1024.0 AS SizeMb, physical_name FROM sys.database_files", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var typeDesc = reader.GetString(0);
                var sizeMb = reader.GetDouble(1);
                var path = reader.GetString(2);
                if (string.IsNullOrEmpty(physicalPath))
                    physicalPath = Path.GetDirectoryName(path) ?? string.Empty;

                switch (typeDesc)
                {
                    case "ROWS": dataMb += sizeMb; break;
                    case "LOG": logMb += sizeMb; break;
                    case "FILESTREAM": fsMb += sizeMb; break;
                }
            }
        }

        return new SageDatabaseInfo
        {
            DatabaseName = database,
            HasComptabilite = installedModules.Contains("CPTA"),
            HasGestionCommerciale = installedModules.Contains("CIAL"),
            HasTresorerie = installedModules.Contains("TRES"),
            HasImmobilisations = installedModules.Contains("IMMO"),
            DataSizeMb = Math.Round(dataMb, 1),
            LogSizeMb = Math.Round(logMb, 1),
            FileStreamSizeMb = Math.Round(fsMb, 1),
            PhysicalPath = physicalPath
        };
    }

    /// <summary>
    /// Modules Sage installés (CPTA/CIAL/TRES/IMMO), lus depuis cbsystable.CB_TYPE
    /// (portage fidèle de reset_admin_password/generate_connexion_files, V1). Retourne
    /// un ensemble vide si cbsystable est absente (base non-Sage).
    /// </summary>
    public static async Task<HashSet<string>> GetInstalledModulesAsync(SqlConnection conn, SqlTransaction? tx = null)
    {
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!await ObjectExistsAsync(conn, conn.Database, "cbsystable", tx))
            return modules;

        await using var cmd = new SqlCommand("SELECT DISTINCT CB_TYPE FROM dbo.cbsystable", conn, tx);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
                modules.Add(reader.GetString(0).Trim());
        }
        return modules;
    }

    /// <summary>
    /// Nombre de connexions actives sur la base, hors la connexion "master" utilisée par
    /// l'outil lui-même pour piloter l'optimisation (sharedConnection, elle-même connectée
    /// à master, pas à la base cible, donc jamais comptée ici de toute façon). Utilisé pour
    /// bloquer "Optimiser la base" si quelqu'un d'autre est connecté.
    /// </summary>
    public async Task<List<string>> GetActiveConnectionsAsync(string instance, string database)
    {
        await using var conn = DatabaseManager.Instance.CreateConnection(instance, "master");
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            @"SELECT s.login_name, s.host_name, s.program_name
              FROM sys.dm_exec_sessions s
              WHERE s.database_id = DB_ID(@db) AND s.session_id <> @@SPID", conn);
        cmd.Parameters.AddWithValue("@db", database);
        await using var reader = await cmd.ExecuteReaderAsync();
        var sessions = new List<string>();
        while (await reader.ReadAsync())
        {
            var login = reader.IsDBNull(0) ? "?" : reader.GetString(0);
            var host = reader.IsDBNull(1) ? "?" : reader.GetString(1);
            var program = reader.IsDBNull(2) ? "?" : reader.GetString(2);
            sessions.Add($"{login}@{host} ({program})");
        }
        return sessions;
    }

    /// <summary>
    /// AUTO_CLOSE OFF, RECOVERY SIMPLE, ajustement du COMPATIBILITY_LEVEL à la version
    /// SQL Server réellement installée, activation FILESTREAM (accès non transactionnel
    /// complet, répertoire = nom de la base), correction des noms logiques de fichiers
    /// (ROWS -> nom de la base, LOG -> nom de la base + "_Log" — un incident réel a montré
    /// des bases copiées depuis un modèle "GEDEV" gardant ce nom logique au lieu du nom
    /// réel), puis — seulement si la base est une base Sage (cbsystable présente) — ALTER
    /// AUTHORIZATION vers sa et réassociation USER_CBASE -> login APPL_CBASE. Portage
    /// étendu de maj_db/maj_all_db (V1). Nécessite une connexion autocommit (ALTER
    /// DATABASE ne peut pas s'exécuter dans une transaction explicite).
    /// </summary>
    public async Task OptimizeDatabaseAsync(string instance, string database, SqlConnection? sharedConnection = null)
    {
        var conn = sharedConnection ?? await DatabaseManager.Instance.OpenAutocommitConnectionAsync(instance, "master");
        try
        {
            var quoted = QuoteName(database);
            await ExecAsync(conn, $"ALTER DATABASE {quoted} SET AUTO_CLOSE OFF WITH NO_WAIT");
            await ExecAsync(conn, $"ALTER DATABASE {quoted} SET RECOVERY SIMPLE WITH NO_WAIT");

            var compatLevel = await GetCompatibilityLevelAsync(conn);
            await ExecAsync(conn, $"ALTER DATABASE {quoted} SET COMPATIBILITY_LEVEL = {compatLevel}");

            await EnableFilestreamAsync(conn, database, quoted);
            await FixLogicalFileNamesAsync(conn, database);

            if (await ObjectExistsAsync(conn, database, "cbsystable"))
            {
                await ExecAsync(conn, $"ALTER AUTHORIZATION ON DATABASE::{quoted} TO [sa]");
                await ExecAsync(conn, $"USE {quoted}; ALTER USER [USER_CBASE] WITH LOGIN = [APPL_CBASE];");
            }
        }
        finally
        {
            if (sharedConnection is null)
                await conn.DisposeAsync();
        }
    }

    /// <summary>
    /// Active FILESTREAM en accès non transactionnel complet (NON_TRANSACTED_ACCESS =
    /// FULL) avec DIRECTORY_NAME = nom de la base. S'assure d'abord que l'instance
    /// autorise l'accès FILESTREAM complet (sp_configure 'filestream access level' = 2) ;
    /// si le service SQL Server n'a jamais eu FILESTREAM activé au niveau Windows (SQL
    /// Server Configuration Manager), cette étape échoue et l'erreur est simplement
    /// remontée (pas de tentative de contourner une configuration Windows depuis l'outil).
    /// </summary>
    private static async Task EnableFilestreamAsync(SqlConnection conn, string database, string quotedDatabase)
    {
        await using (var checkCmd = new SqlCommand(
            "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'filestream access level'", conn))
        {
            var level = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (level < 2)
            {
                await ExecAsync(conn, "EXEC sp_configure 'filestream access level', 2");
                await ExecAsync(conn, "RECONFIGURE");
            }
        }

        // Timeout borné (contrairement aux autres ALTER DATABASE de cette méthode) : cette
        // commande nécessite un accès exclusif et reste bloquée tant que des sessions sont
        // connectées à la base — constaté en conditions réelles. La vérification "aucun
        // utilisateur connecté" côté UI doit empêcher ce cas, mais un filet de sécurité
        // évite un blocage indéfini de l'application en cas de connexion concurrente.
        await using var filestreamCmd = new SqlCommand(
            $"ALTER DATABASE {quotedDatabase} SET FILESTREAM (NON_TRANSACTED_ACCESS = FULL, DIRECTORY_NAME = N'{database.Replace("'", "''")}')",
            conn) { CommandTimeout = 15 };
        await filestreamCmd.ExecuteNonQueryAsync();
    }

    /// <summary>Renomme les fichiers logiques ROWS/LOG pour qu'ils correspondent au nom réel de la base.</summary>
    private static async Task FixLogicalFileNamesAsync(SqlConnection conn, string database)
    {
        var expectedRows = database;
        var expectedLog = database + "_Log";

        await using var cmd = new SqlCommand(
            "SELECT name, type_desc FROM sys.master_files WHERE database_id = DB_ID(@db) AND type_desc IN ('ROWS', 'LOG')", conn);
        cmd.Parameters.AddWithValue("@db", database);

        var renames = new List<(string CurrentName, string NewName)>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var currentName = reader.GetString(0);
                var typeDesc = reader.GetString(1);
                var expected = typeDesc == "ROWS" ? expectedRows : expectedLog;
                if (!string.Equals(currentName, expected, StringComparison.OrdinalIgnoreCase))
                    renames.Add((currentName, expected));
            }
        }

        foreach (var (currentName, newName) in renames)
        {
            await ExecAsync(conn,
                $"ALTER DATABASE {QuoteName(database)} MODIFY FILE (NAME = N'{currentName.Replace("'", "''")}', NEWNAME = N'{newName.Replace("'", "''")}')");
        }
    }

    /// <summary>
    /// Portage de get_sql_version (V1), avec un changement : le niveau de compatibilité
    /// suit directement la version majeure de SQL Server (majorVersion * 10), sans
    /// plafond arbitraire. Une tentative précédente plafonnait à 160 en pensant que les
    /// niveaux plus récents (170, SQL Server 2025) n'étaient pas reconnus par Sage — cette
    /// hypothèse s'est révélée fausse (la cause d'un incident "Mauvaise version de
    /// dossier" était en réalité un BOM UTF-8 dans un fichier de connexion généré, sans
    /// rapport avec le niveau de compatibilité). Le niveau natif de l'instance est donc
    /// bien le bon choix.
    /// </summary>
    private static async Task<int> GetCompatibilityLevelAsync(SqlConnection conn)
    {
        await using var cmd = new SqlCommand(
            "SELECT LEFT(CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR(50)), 2)", conn);
        var result = await cmd.ExecuteScalarAsync();
        if (result is string s && int.TryParse(s, out var majorVersion) && majorVersion >= 8)
            return majorVersion * 10;
        return 110;
    }

    private static async Task ExecAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        await cmd.ExecuteNonQueryAsync();
    }

    private static string QuoteName(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    /// <summary>
    /// Triggers non standards (#8). Filtre exact porté de list_triggers (V1) — note que
    /// TG_CBUPD% n'a volontairement pas de underscore après UPD, contrairement à
    /// TG_CBDEL_%/TG_CBINS_%.
    /// </summary>
    public async Task<List<TriggerInfo>> ListNonStandardTriggersAsync()
    {
        const string sql = @"
            SELECT OBJECT_NAME(tr.parent_id) AS TableName, tr.name AS TriggerName
            FROM sys.triggers tr
            WHERE tr.name NOT LIKE 'TG_CBDEL[_]%'
              AND tr.name NOT LIKE 'TG_CBINS[_]%'
              AND tr.name NOT LIKE 'TG_CBUPD%'
              AND tr.name NOT LIKE 'TG_INS[_]%'
              AND tr.name NOT LIKE 'TG_UPD[_]%'
              AND tr.name NOT LIKE 'TG_DEL[_]%'
            ORDER BY tr.name";

        var table = await DatabaseManager.Instance.ExecuteQueryAsync(sql);
        return table.Rows.Cast<DataRow>()
            .Select(r => new TriggerInfo
            {
                TableName = r["TableName"] as string ?? string.Empty,
                TriggerName = (string)r["TriggerName"]
            })
            .ToList();
    }

    /// <summary>
    /// Colonnes "intitulé" ciblées par la correction d'accents (#15). Noms de colonnes
    /// non couverts par le source V1 (fonctionnalité ajoutée en V2) ni par la
    /// vérification PDF — convention Sage standard, à confirmer sur une base réelle
    /// avant premier usage.
    /// </summary>
    private static readonly (string Table, string Column)[] EncodingTargets =
    {
        ("F_ECRITUREC", "EC_Libelle"),
        ("F_COMPTEG", "CG_Intitule"),
        ("F_COMPTET", "CT_Intitule"),
        ("F_COMPTEA", "CA_Intitule"),
    };

    private static readonly (string Bad, string Good)[] MojibakeReplacements =
    {
        ("Ã©", "é"), ("Ã¨", "è"), ("Ãª", "ê"), ("Ã«", "ë"),
        ("Ã ", "à"), ("Ã¢", "â"), ("Ã®", "î"), ("Ã¯", "ï"),
        ("Ã´", "ô"), ("Ã¹", "ù"), ("Ã»", "û"), ("Ã§", "ç"),
        ("Ã‰", "É"), ("Ã€", "À"), ("â€™", "'"), ("â€“", "-"),
    };

    public async Task<Dictionary<string, int>> ScanEncodingIssuesAsync()
    {
        var counts = new Dictionary<string, int>();
        foreach (var (table, column) in EncodingTargets)
        {
            if (!await TableColumnExistsAsync(table, column)) continue;

            var sql = $"SELECT COUNT(*) FROM {QuoteName(table)} WHERE {QuoteName(column)} LIKE '%Ã%' OR {QuoteName(column)} LIKE '%â€%'";
            var result = await DatabaseManager.Instance.ExecuteScalarAsync(sql);
            var count = result is int i ? i : Convert.ToInt32(result);
            if (count > 0) counts[$"{table}.{column}"] = count;
        }
        return counts;
    }

    public async Task<int> FixEncodingAsync()
    {
        var totalUpdated = 0;
        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            foreach (var (table, column) in EncodingTargets)
            {
                if (!await TableColumnExistsAsync(table, column)) continue;

                var expr = QuoteName(column);
                foreach (var (bad, good) in MojibakeReplacements)
                    expr = $"REPLACE({expr}, N'{bad.Replace("'", "''")}', N'{good.Replace("'", "''")}')";

                var sql = $"UPDATE {QuoteName(table)} SET {QuoteName(column)} = {expr} " +
                           $"WHERE {QuoteName(column)} LIKE '%Ã%' OR {QuoteName(column)} LIKE '%â€%'";

                totalUpdated += await DatabaseManager.ExecuteInTransactionAsync(conn, tx, sql);
            }
        });
        return totalUpdated;
    }

    private static async Task<bool> TableColumnExistsAsync(string table, string column)
    {
        const string sql = @"
            SELECT COUNT(*) FROM sys.columns c
            JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = @table AND c.name = @column";
        var result = await DatabaseManager.Instance.ExecuteScalarAsync(sql, new Dictionary<string, object?>
        {
            ["@table"] = table,
            ["@column"] = column
        });
        return Convert.ToInt32(result) > 0;
    }
}
