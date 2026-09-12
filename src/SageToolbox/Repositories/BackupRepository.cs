using System.IO;
using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Repositories;

/// <summary>
/// Sauvegarde en lot de toutes les bases utilisateur d'une instance (hors bases système
/// master/model/msdb/tempdb), symétrique de RestorationRepository. Contrairement aux
/// autres opérations "Serveur &amp; Instance" (qui ne ciblent que les bases Sage détectées
/// via cbsystable), la sauvegarde couvre volontairement TOUTES les bases utilisateur,
/// Sage ou non — c'est la demande explicite : mieux vaut une sauvegarde de trop qu'une
/// base oubliée.
/// </summary>
public sealed class BackupRepository
{
    public sealed record BackupResult(string DatabaseName, bool Success, string? ErrorMessage);

    public async Task<List<string>> GetAllUserDatabasesAsync(string instance)
    {
        await using var conn = DatabaseManager.Instance.CreateConnection(instance, "master");
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0 ORDER BY name", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<string>();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0));
        return list;
    }

    /// <summary>Une erreur sur une base n'interrompt pas la sauvegarde des suivantes.</summary>
    public async Task<List<BackupResult>> BackupAllAsync(string instance, string backupFolder, bool useCompression)
    {
        Directory.CreateDirectory(backupFolder);
        var databases = await GetAllUserDatabasesAsync(instance);
        var results = new List<BackupResult>();
        var total = databases.Count;

        for (var i = 0; i < total; i++)
        {
            var database = databases[i];
            Logger.Instance.Info($"[{i + 1}/{total}] Sauvegarde de {database}...");
            try
            {
                var fileName = $"{database}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                var path = Path.Combine(backupFolder, fileName);
                await BackupOneAsync(instance, database, path, useCompression);
                Logger.Instance.Success($"  {database} : sauvegardée -> {fileName}");
                results.Add(new BackupResult(database, true, null));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"  {database} : échec — {ex.Message}");
                results.Add(new BackupResult(database, false, ex.Message));
            }
        }

        return results;
    }

    private static async Task BackupOneAsync(string instance, string database, string backupFilePath, bool useCompression)
    {
        await using var conn = DatabaseManager.Instance.CreateConnection(instance, database);
        await conn.OpenAsync();
        var compressionClause = useCompression ? ", COMPRESSION" : "";
        await using var cmd = new SqlCommand(
            $"BACKUP DATABASE {DatabaseManager.QuoteIdentifier(database)} TO DISK = @path WITH INIT{compressionClause}",
            conn) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@path", backupFilePath);
        await cmd.ExecuteNonQueryAsync();
    }
}
