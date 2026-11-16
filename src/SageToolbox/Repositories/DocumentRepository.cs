using SageToolbox.Core;
using SageToolbox.Models;

namespace SageToolbox.Repositories;

/// <summary>
/// Dévalidation de factures de vente. Clé réelle d'un document = (DO_Type, DO_Piece) (index
/// unique IDO_TPIECE, PDF p.242-266) ; F_DOCLIGNE porte aussi DO_Type en clé dénormalisée
/// (index IDL_LIGNE), d'où la mise à jour sur les deux tables.
/// Seules les factures validées non comptabilisées (DO_Type 6, DO_Valide 1) sont proposées ;
/// une facture transmise en facturation électronique (F_ECDOCDN.DN_Statut &gt; 0) existe côté
/// administration fiscale et ne peut plus être dévalidée.
/// </summary>
public sealed class DocumentRepository
{
    public const int MaxResults = 500;

    public async Task<(List<FactureValidee> Factures, bool Truncated)> GetFacturesValideesAsync(
        DateTime? dateDebut, DateTime? dateFin, string client, string piece)
    {
        var hasEInvoice = await HasEInvoiceTableAsync();
        var transmise = hasEInvoice
            ? "CASE WHEN EXISTS (SELECT 1 FROM {schema}.F_ECDOCDN dn WHERE dn.DO_Type = de.DO_Type AND dn.DO_Piece = de.DO_Piece AND dn.DN_Statut > 0) THEN 1 ELSE 0 END"
            : "0";

        var where = new List<string> { "de.DO_Domaine = 0", "de.DO_Type = 6", "de.DO_Valide = 1" };
        var parameters = new Dictionary<string, object?>();
        if (dateDebut is not null)
        {
            where.Add("de.DO_Date >= @debut");
            parameters["@debut"] = dateDebut.Value.Date;
        }
        if (dateFin is not null)
        {
            where.Add("de.DO_Date < @finExclu");
            parameters["@finExclu"] = dateFin.Value.Date.AddDays(1);
        }
        if (client.Length > 0)
        {
            where.Add("(de.DO_Tiers LIKE @client OR ct.CT_Intitule LIKE @client)");
            parameters["@client"] = $"%{EscapeLike(client)}%";
        }
        if (piece.Length > 0)
        {
            where.Add("de.DO_Piece LIKE @piece");
            parameters["@piece"] = $"%{EscapeLike(piece)}%";
        }

        var sql = $@"
            SELECT TOP {MaxResults + 1} de.DO_Type, de.DO_Piece, de.DO_Date, de.DO_Tiers,
                   ISNULL(ct.CT_Intitule, '') AS CT_Intitule, de.DO_TotalHT, {transmise} AS Transmise
            FROM {{schema}}.F_DOCENTETE de
            LEFT JOIN {{schema}}.F_COMPTET ct ON ct.CT_Num = de.DO_Tiers
            WHERE {string.Join(" AND ", where)}
            ORDER BY de.DO_Date DESC, de.DO_Piece DESC";

        var table = await DatabaseManager.Instance.ExecuteQueryAsync(sql, parameters);
        var factures = table.Rows.Cast<System.Data.DataRow>().Select(r => new FactureValidee
        {
            DoType = Convert.ToInt32(r["DO_Type"]),
            DoPiece = ((string)r["DO_Piece"]).Trim(),
            DoDate = Convert.ToDateTime(r["DO_Date"]),
            CtNum = ((string)r["DO_Tiers"]).Trim(),
            CtIntitule = ((string)r["CT_Intitule"]).Trim(),
            TotalHt = Convert.ToDecimal(r["DO_TotalHT"]),
            EnvoyeeElectroniquement = Convert.ToInt32(r["Transmise"]) != 0
        }).ToList();

        var truncated = factures.Count > MaxResults;
        if (truncated) factures.RemoveAt(factures.Count - 1);
        return (factures, truncated);
    }

    /// <summary>Dévalide une facture. L'état (validée, non transmise) est revérifié dans la
    /// transaction : la mise à jour ne touche aucune ligne si la facture a changé entre-temps.</summary>
    public async Task DevaliderAsync(FactureValidee facture)
    {
        var hasEInvoice = await HasEInvoiceTableAsync();
        var notSent = hasEInvoice
            ? " AND NOT EXISTS (SELECT 1 FROM {schema}.F_ECDOCDN dn WHERE dn.DO_Type = @type AND dn.DO_Piece = @piece AND dn.DN_Statut > 0)"
            : string.Empty;
        var parameters = new Dictionary<string, object?> { ["@type"] = facture.DoType, ["@piece"] = facture.DoPiece };

        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            var updated = await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                "UPDATE {schema}.F_DOCENTETE SET DO_Valide = 0, DO_Imprim = 0, DO_Type = 6 " +
                "WHERE DO_Domaine = 0 AND DO_Type = @type AND DO_Piece = @piece AND DO_Valide = 1" + notSent,
                parameters);

            if (updated != 1)
            {
                var sent = hasEInvoice && Convert.ToInt32(await DatabaseManager.Instance.ExecuteScalarAsync(
                    "SELECT COUNT(*) FROM {schema}.F_ECDOCDN WHERE DO_Type = @type AND DO_Piece = @piece AND DN_Statut > 0",
                    parameters)) > 0;
                throw new InvalidOperationException(sent
                    ? FactureValidee.MotifEnvoiElectronique
                    : "La facture n'est plus validée (déjà dévalidée ou comptabilisée entre-temps).");
            }

            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                "UPDATE {schema}.F_DOCLIGNE SET DO_Type = 6 WHERE DO_Type = @type AND DO_Piece = @piece",
                parameters);
        });
    }

    private static async Task<bool> HasEInvoiceTableAsync()
    {
        var id = await DatabaseManager.Instance.ExecuteScalarAsync("SELECT OBJECT_ID('{schema}.F_ECDOCDN')");
        return id is not null and not DBNull;
    }

    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
