using Microsoft.Data.SqlClient;
using SageToolbox.Core;

namespace SageToolbox.Repositories;

/// <summary>
/// Réinitialisation du mot de passe administrateur (#16) et purge des sessions
/// fantômes (#17). Portage fidèle de reset_admin_password/cleanup_sessions
/// (AIG_Toolbox.py V1) — GUID/hash par défaut et colonnes cb* copiés à l'identique
/// depuis le source original.
/// </summary>
public sealed class AdminRepository
{
    private const string DefaultGuid = "d17b5a69-8dd6-42fd-805c-36c5c7419872";
    private const string DefaultHashLiteral = "0x870A3CFBA667D3B6A8B40BA44545F63D2B2012F7A697A43AFD3AA753F618DDE2";

    private static readonly (string CbType, string Table, string Label)[] ModuleTables =
    {
        ("CPTA", "F_PROTECTIONCPTA", "Comptabilité"),
        ("CIAL", "F_PROTECTIONCIAL", "Gestion Commerciale"),
        ("TRES", "F_PROTECTIONTRES", "Moyens de Paiement/Trésorerie"),
        ("IMMO", "F_PROTECTIONIMMO", "Immobilisation"),
    };

    private static readonly (string CbType, string Table)[] SecurityStrategyTables =
    {
        ("CPTA", "P_SECURITYSTRATEGYCPTA"),
        ("CIAL", "P_SECURITYSTRATEGYCIAL"),
        ("IMMO", "P_SECURITYSTRATEGYIMMO"),
        ("TRES", "P_SECURITYSTRATEGYTRES"),
    };

    public sealed record SecurityOption(string Column, string Label);

    /// <summary>
    /// Options de la politique de mot de passe (colonnes P_SECURITYSTRATEGYxxx,
    /// confirmées sur base réelle : une seule ligne par table, flags 0/1).
    /// </summary>
    public static readonly SecurityOption[] SecurityOptions =
    {
        new("SECUR_PwdStrong", "Définir un mot de passe fort sur au moins 8 caractères"),
        new("SECUR_PwdRenouv", "Renouveler tous les 90 jours"),
        new("SECUR_PwdComplex", "Définir un mot de passe différent du nom d'utilisateur"),
        new("SECUR_PwdChain", "Définir un mot de passe identique dans tous les fichiers liés"),
        new("SECUR_PwdRequired", "Saisie du mot de passe obligatoire"),
    };

    /// <summary>
    /// Réinitialise le mot de passe administrateur (PROT_NO=1) sur chaque module Sage
    /// installé, et désactive (met à 0) les colonnes de politique de mot de passe
    /// sélectionnées dans <paramref name="securityColumnsToDisable"/> sur les tables
    /// P_SECURITYSTRATEGYxxx des mêmes modules.
    /// </summary>
    public async Task<List<string>> ResetAdminPasswordAsync(IReadOnlyCollection<string> securityColumnsToDisable)
    {
        var affected = new List<string>();
        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            var modules = await ServerRepository.GetInstalledModulesAsync(conn, tx);

            foreach (var (cbType, table, label) in ModuleTables)
            {
                if (!modules.Contains(cbType)) continue;

                var sql = $"UPDATE dbo.{DatabaseManager.QuoteIdentifier(table)} " +
                          $"SET PROT_Guid = @guid, PROT_Hash = {DefaultHashLiteral} WHERE PROT_NO = 1";
                var rows = await DatabaseManager.ExecuteInTransactionAsync(conn, tx, sql,
                    new Dictionary<string, object?> { ["@guid"] = DefaultGuid });
                if (rows > 0) affected.Add(label);
            }

            if (securityColumnsToDisable.Count > 0)
            {
                var setClause = string.Join(", ", securityColumnsToDisable.Select(c => $"{DatabaseManager.QuoteIdentifier(c)} = 0"));
                foreach (var (cbType, table) in SecurityStrategyTables)
                {
                    if (!modules.Contains(cbType)) continue;
                    await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                        $"UPDATE dbo.{DatabaseManager.QuoteIdentifier(table)} SET {setClause}");
                }
            }
        });
        return affected;
    }

    /// <summary>
    /// Sessions Sage (cbUserSession) dont le SPID SQL Server n'existe plus, hors
    /// sessions système (CB_Creator = 'COBJ'). sys.sysprocesses est une vue de
    /// compatibilité historique mais reste la source utilisée par l'original — conservée
    /// pour fidélité de comportement.
    /// </summary>
    public async Task<List<string>> PurgeGhostSessionsAsync()
    {
        var ghostUsers = new List<string>();

        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            await using (var selectCmd = new SqlCommand(
                @"SELECT cbUsername FROM dbo.cbUserSession
                  WHERE cbSession NOT IN (SELECT spid FROM sys.sysprocesses WHERE dbid = DB_ID())
                    AND CB_Creator <> 'COBJ'", conn, tx))
            await using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    ghostUsers.Add(reader.IsDBNull(0) ? "Inconnu" : reader.GetString(0));
            }

            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                @"DELETE FROM dbo.cbUserSession
                  WHERE cbSession NOT IN (SELECT spid FROM sys.sysprocesses WHERE dbid = DB_ID())
                    AND CB_Creator <> 'COBJ'");

            foreach (var table in new[] { "cbMessage", "cbNotification", "cbRegFile", "cbRegMessage", "cbRegUser" })
            {
                await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                    $"DELETE FROM dbo.{DatabaseManager.QuoteIdentifier(table)} " +
                    "WHERE cbSession NOT IN (SELECT cbSession FROM dbo.cbUserSession)");
            }
        });

        return ghostUsers;
    }
}
