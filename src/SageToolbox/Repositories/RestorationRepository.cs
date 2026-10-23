using System.IO;
using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Repositories;

/// <summary>
/// Restauration en lot de bases SQL Server depuis un dossier de fichiers .bak. Portage
/// fonctionnel depuis RestaurationSQL.md (synthèse par décompilation de l'outil d'origine
/// RestaurationSQL.exe, .NET Framework, sources perdues), étendu à la demande :
///   - les fichiers physiques (.mdf/.ldf/FILESTREAM) sont redirigés vers les répertoires
///     par défaut de l'INSTANCE (SERVERPROPERTY('InstanceDefaultDataPath'/'InstanceDefaultLogPath')),
///     plus besoin de les saisir manuellement ;
///   - fichiers physiques et noms logiques sont renommés d'après le nom de la base
///     restaurée, et FILESTREAM (si présent) passe en accès complet, répertoire = nom de
///     la base ;
///   - chaque base restaurée est automatiquement optimisée (mêmes requêtes que le bouton
///     "Optimiser la base" — AUTO_CLOSE, RECOVERY, niveau de compatibilité,
///     réassociation USER_CBASE/APPL_CBASE) ;
///   - les fichiers de connexion de chaque base restaurée sont générés automatiquement.
///
/// Différence avec l'original : les colonnes de RESTORE HEADERONLY/FILELISTONLY sont
/// lues par NOM (via DataReader) plutôt que par position, ce qui évite toute adaptation
/// spécifique à la version de SQL Server.
/// </summary>
public sealed class RestorationRepository
{
    private readonly ServerRepository _serverRepository = new();
    private readonly ConnectionFileRepository _connectionFileRepository = new();

    public enum RestoreOutcome { Restored, Replaced, AlreadyExists, Failed }

    public sealed record BakFileResult(string FileName, string DatabaseName, RestoreOutcome Outcome, string? ErrorMessage);

    /// <summary>
    /// Restaure tous les fichiers .bak d'un dossier. Une erreur sur un fichier n'interrompt
    /// pas le traitement des suivants (portage fidèle du comportement d'origine).
    /// </summary>
    /// <param name="connFilesFolder">Dossier où générer les fichiers de connexion des bases restaurées.</param>
    public async Task<List<BakFileResult>> RestoreAllAsync(
        string instance, string bakFolder, string connFilesFolder, bool replaceExisting)
    {
        var bakFiles = Directory.GetFiles(bakFolder, "*.bak", SearchOption.TopDirectoryOnly);
        var results = new List<BakFileResult>();
        var total = bakFiles.Length;

        for (var i = 0; i < total; i++)
        {
            var bakFile = bakFiles[i];
            var fileName = Path.GetFileName(bakFile);
            Logger.Instance.Info($"[{i + 1}/{total}] {fileName}...");

            try
            {
                var result = await RestoreOneAsync(instance, bakFile, replaceExisting);
                results.Add(result);

                switch (result.Outcome)
                {
                    case RestoreOutcome.AlreadyExists:
                        Logger.Instance.Warning($"  {result.DatabaseName} : existe déjà, ignorée.");
                        continue;
                    case RestoreOutcome.Replaced:
                        Logger.Instance.Success($"  {result.DatabaseName} : restaurée (remplacement).");
                        break;
                    case RestoreOutcome.Restored:
                        Logger.Instance.Success($"  {result.DatabaseName} : restaurée.");
                        break;
                }

                await OptimizeAfterRestoreAsync(instance, result.DatabaseName);
                await GenerateConnectionFilesAfterRestoreAsync(instance, result.DatabaseName, connFilesFolder);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"  {fileName} : échec — {ex.Message}");
                results.Add(new BakFileResult(fileName, "?", RestoreOutcome.Failed, ex.Message));
            }
        }

        return results;
    }

    private async Task OptimizeAfterRestoreAsync(string instance, string databaseName)
    {
        try
        {
            Logger.Instance.Info($"  Optimisation de {databaseName}...");
            await _serverRepository.OptimizeDatabaseAsync(instance, databaseName);
            Logger.Instance.Success($"  {databaseName} : optimisée.");
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"  Optimisation de {databaseName} échouée : {ex.Message}");
        }
    }

    private async Task GenerateConnectionFilesAfterRestoreAsync(string instance, string databaseName, string connFilesFolder)
    {
        try
        {
            var files = await _connectionFileRepository.GenerateAsync(instance, databaseName, connFilesFolder);
            Logger.Instance.Info($"  {databaseName} : {files.Count} fichier(s) de connexion généré(s).");
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"  Génération des fichiers de connexion de {databaseName} échouée : {ex.Message}");
        }
    }

    private async Task<BakFileResult> RestoreOneAsync(string instance, string bakFilePath, bool replaceExisting)
    {
        var fileName = Path.GetFileName(bakFilePath);

        await using var conn = DatabaseManager.Instance.CreateConnection(instance, "master");
        await conn.OpenAsync();

        var databaseName = await GetDatabaseNameAsync(conn, bakFilePath);
        var exists = await DatabaseExistsAsync(conn, databaseName);

        if (exists && !replaceExisting)
            return new BakFileResult(fileName, databaseName, RestoreOutcome.AlreadyExists, null);

        if (exists)
        {
            // Libère la base (déconnecte les sessions en cours) pour permettre le remplacement.
            await using var offlineCmd = new SqlCommand(
                $"ALTER DATABASE {DatabaseManager.QuoteIdentifier(databaseName)} SET OFFLINE WITH ROLLBACK IMMEDIATE",
                conn) { CommandTimeout = 0 };
            await offlineCmd.ExecuteNonQueryAsync();
        }

        var (dataPath, logPath) = await GetDefaultFilePathsAsync(conn);
        var files = await GetFileListAsync(conn, bakFilePath);

        // Les fichiers physiques et logiques sont nommés d'après le nom de la base restaurée,
        // jamais d'après les noms logiques du .bak : une base copiée depuis une autre (ex.
        // P_200 sauvegardée avec les noms logiques P_620/P_620_log) écraserait sinon les
        // fichiers de la base d'origine ou garderait des noms trompeurs.
        //   1er fichier de données -> {base}.mdf, nom logique {base}
        //   1er journal            -> {base}_log.ldf, nom logique {base}_log
        //   fichiers suivants      -> suffixe _2, _3... (.ndf / .ldf)
        //   FILESTREAM             -> dossier {base} (nom logique conservé)
        var moveClauses = new List<string>();
        var logicalRenames = new List<(string CurrentName, string NewName)>();
        var dataFileIndex = 0;
        var logFileIndex = 0;
        var filestreamIndex = 0;
        foreach (var (logicalName, type) in files)
        {
            string targetPath;
            string? expectedLogicalName = null;
            switch (type)
            {
                case "L":
                    logFileIndex++;
                    expectedLogicalName = logFileIndex == 1 ? databaseName + "_log" : $"{databaseName}_log_{logFileIndex}";
                    targetPath = Path.Combine(logPath, expectedLogicalName + ".ldf");
                    break;
                case "S":
                    // FILESTREAM : la "cible" d'un MOVE est un dossier, pas un fichier.
                    filestreamIndex++;
                    targetPath = Path.Combine(dataPath, filestreamIndex == 1 ? databaseName : $"{databaseName}_{filestreamIndex}");
                    break;
                default:
                    dataFileIndex++;
                    expectedLogicalName = dataFileIndex == 1 ? databaseName : $"{databaseName}_{dataFileIndex}";
                    targetPath = Path.Combine(dataPath, expectedLogicalName + (dataFileIndex == 1 ? ".mdf" : ".ndf"));
                    break;
            }
            moveClauses.Add($"MOVE N'{logicalName.Replace("'", "''")}' TO N'{targetPath.Replace("'", "''")}'");
            if (expectedLogicalName is not null && !string.Equals(logicalName, expectedLogicalName, StringComparison.OrdinalIgnoreCase))
                logicalRenames.Add((logicalName, expectedLogicalName));
        }

        var restoreSql =
            $"RESTORE DATABASE {DatabaseManager.QuoteIdentifier(databaseName)} " +
            $"FROM DISK = @path WITH REPLACE, {string.Join(", ", moveClauses)}";

        await using (var restoreCmd = new SqlCommand(restoreSql, conn) { CommandTimeout = 0 })
        {
            restoreCmd.Parameters.AddWithValue("@path", bakFilePath);
            await restoreCmd.ExecuteNonQueryAsync();
        }

        foreach (var (currentName, newName) in logicalRenames)
        {
            await ExecAsync(conn,
                $"ALTER DATABASE {DatabaseManager.QuoteIdentifier(databaseName)} " +
                $"MODIFY FILE (NAME = N'{currentName.Replace("'", "''")}', NEWNAME = N'{newName.Replace("'", "''")}')");
            Logger.Instance.Info($"  Nom logique {currentName} renommé en {newName}.");
        }

        if (filestreamIndex > 0)
            await EnableFilestreamAsync(conn, databaseName);

        return new BakFileResult(fileName, databaseName, exists ? RestoreOutcome.Replaced : RestoreOutcome.Restored, null);
    }

    /// <summary>
    /// Active FILESTREAM en accès non transactionnel complet (NON_TRANSACTED_ACCESS = FULL)
    /// avec DIRECTORY_NAME = nom de la base. Relève au besoin le niveau d'accès FILESTREAM
    /// de l'instance à 2 ; si FILESTREAM n'est pas activé sur le service Windows (SQL Server
    /// Configuration Manager), l'erreur est remontée telle quelle.
    /// </summary>
    private static async Task EnableFilestreamAsync(SqlConnection conn, string databaseName)
    {
        await using (var checkCmd = new SqlCommand(
            "SELECT CAST(value_in_use AS INT) FROM sys.configurations WHERE name = 'filestream access level'", conn))
        {
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) < 2)
            {
                await ExecAsync(conn, "EXEC sp_configure 'filestream access level', 2");
                await ExecAsync(conn, "RECONFIGURE");
            }
        }

        // Timeout borné : la commande exige un accès exclusif à la base et resterait bloquée
        // indéfiniment si une session s'y connectait entre-temps.
        await using var cmd = new SqlCommand(
            $"ALTER DATABASE {DatabaseManager.QuoteIdentifier(databaseName)} " +
            $"SET FILESTREAM (NON_TRANSACTED_ACCESS = FULL, DIRECTORY_NAME = N'{databaseName.Replace("'", "''")}')",
            conn) { CommandTimeout = 15 };
        await cmd.ExecuteNonQueryAsync();
        Logger.Instance.Info($"  FILESTREAM activé (accès complet, répertoire {databaseName}).");
    }

    private static async Task ExecAsync(SqlConnection conn, string sql)
    {
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Répertoires par défaut de l'instance pour les fichiers de données/journal.</summary>
    private static async Task<(string DataPath, string LogPath)> GetDefaultFilePathsAsync(SqlConnection conn)
    {
        await using var cmd = new SqlCommand(
            @"SELECT
                CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(500)) AS DataPath,
                CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS NVARCHAR(500)) AS LogPath", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Impossible de déterminer les répertoires par défaut de l'instance.");

        var dataPath = reader["DataPath"] as string;
        if (string.IsNullOrWhiteSpace(dataPath))
            throw new InvalidOperationException("Répertoire de données par défaut de l'instance introuvable.");
        var logPath = reader["LogPath"] as string;
        if (string.IsNullOrWhiteSpace(logPath))
            logPath = dataPath;

        return (dataPath, logPath);
    }

    private static async Task<string> GetDatabaseNameAsync(SqlConnection conn, string bakFilePath)
    {
        await using var cmd = new SqlCommand("RESTORE HEADERONLY FROM DISK = @path", conn) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@path", bakFilePath);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Impossible de lire l'entête du fichier .bak (RESTORE HEADERONLY).");
        var name = reader["DatabaseName"] as string;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Nom de base introuvable dans l'entête du .bak.");
        return name.Replace(" ", "_");
    }

    private static async Task<List<(string LogicalName, string Type)>> GetFileListAsync(SqlConnection conn, string bakFilePath)
    {
        await using var cmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @path", conn) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@path", bakFilePath);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<(string, string)>();
        while (await reader.ReadAsync())
            list.Add(((string)reader["LogicalName"], (string)reader["Type"]));
        return list;
    }

    private static async Task<bool> DatabaseExistsAsync(SqlConnection conn, string databaseName)
    {
        await using var cmd = new SqlCommand("SELECT 1 FROM sys.databases WHERE name = @db", conn);
        cmd.Parameters.AddWithValue("@db", databaseName);
        return await cmd.ExecuteScalarAsync() is not null;
    }
}
