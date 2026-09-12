using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Repositories;

/// <summary>
/// Copie des utilisateurs/droits entre deux bases (#20). Portage fidèle de
/// check_tables_existance/check_protno_in_f_collaborateur/execute_users_copy
/// (AIG_Toolbox.py V1), y compris les listes de colonnes exactes de F_PROTECTIONxxx et
/// F_Exxx (INSERT par liste explicite plutôt que SELECT *, pour ne pas dépendre de
/// l'ordre des colonnes entre bases).
/// </summary>
public sealed class UsersRepository
{
    private static readonly string[] BaseTables =
    {
        "F_PROTECTIONCPTA", "F_PROTECTIONCIAL", "F_PROTECTIONIMMO", "F_PROTECTIONTRES"
    };

    private static readonly string[] ProtectionColumns =
    {
        "PROT_USER", "PROT_DESCRIPTION", "PROT_RIGHT", "PROT_NO", "PROT_EMAIL", "PROT_USERPROFIL",
        "PROT_ADMINISTRATOR", "PROT_PWDSTATUS", "PROT_DISABLED", "PROT_APPLICATIONRIGHT",
        "PROT_ALLOWEXTERNALACCESS", "PROT_GUID", "PROT_HASH", "PROT_PROVIDERTYPE",
        "PROT_PROVIDERID_AAD", "PROT_PROVIDERID_AD", "PROT_SERVICEOFFICE365ACTIF",
        "PROT_SERVICEOFFICE365USERID", "PROT_SERVICESAGEIDACTIF", "PROT_SERVICESAGEIDUSERID"
    };

    private static readonly string[] ExtendedColumns = { "PROT_NO", "EPROT_Cmd", "EPROT_RIGHT" };

    public async Task<List<string>> GetExistingProtectionTablesAsync(string database)
    {
        var existing = new List<string>();
        foreach (var table in BaseTables)
        {
            if (await DatabaseManager.Instance.ObjectExistsAsync(table, database))
                existing.Add(table);
        }
        return existing;
    }

    public async Task<int> CountLinkedCollaborateursAsync(string database)
    {
        if (!await DatabaseManager.Instance.ObjectExistsAsync("F_COLLABORATEUR", database) ||
            !await DatabaseManager.Instance.ColumnExistsAsync("F_COLLABORATEUR", "PROT_NO", database))
            return 0;

        var result = await DatabaseManager.Instance.ExecuteScalarAsync(
            "SELECT COUNT(*) FROM dbo.F_COLLABORATEUR WHERE PROT_NO IS NOT NULL", databaseOverride: database);
        return Convert.ToInt32(result);
    }

    public async Task UnlinkCollaborateursAsync(string database) =>
        await DatabaseManager.Instance.ExecuteNonQueryAsync(
            "UPDATE dbo.F_COLLABORATEUR SET PROT_NO = NULL WHERE PROT_NO IS NOT NULL", databaseOverride: database);

    public async Task<int> CopyUsersAsync(string sourceDatabase, string destDatabase, List<string> tables)
    {
        if (string.Equals(sourceDatabase, destDatabase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La base source et la base destination doivent être différentes.");

        var protectionCols = string.Join(", ", ProtectionColumns.Select(DatabaseManager.QuoteIdentifier));
        var extendedCols = string.Join(", ", ExtendedColumns.Select(DatabaseManager.QuoteIdentifier));
        var sourceQuoted = DatabaseManager.QuoteIdentifier(sourceDatabase);
        var totalRows = 0;

        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            foreach (var table in tables)
            {
                var tableQuoted = DatabaseManager.QuoteIdentifier(table);
                var eTableQuoted = DatabaseManager.QuoteIdentifier("F_E" + table[2..]);

                await DatabaseManager.ExecuteInTransactionAsync(conn, tx, $"DELETE FROM dbo.{eTableQuoted}");
                await DatabaseManager.ExecuteInTransactionAsync(conn, tx, $"DELETE FROM dbo.{tableQuoted} WHERE PROT_NO <> 1");

                totalRows += await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                    $"INSERT INTO dbo.{tableQuoted} ({protectionCols}) " +
                    $"SELECT {protectionCols} FROM {sourceQuoted}.dbo.{tableQuoted} WHERE PROT_NO <> 1");

                totalRows += await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                    $"INSERT INTO dbo.{eTableQuoted} ({extendedCols}) " +
                    $"SELECT {extendedCols} FROM {sourceQuoted}.dbo.{eTableQuoted}");
            }
        }, databaseOverride: destDatabase);

        return totalRows;
    }
}
