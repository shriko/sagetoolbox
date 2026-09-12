using SageToolbox.Core;
using SageToolbox.Models;

namespace SageToolbox.Repositories;

/// <summary>
/// Dévalidation de facture (#12). Colonnes confirmées PDF p.242-266 : clé réelle d'un
/// document = (DO_Type, DO_Piece) (index unique IDO_TPIECE) — DO_Piece seul n'est pas
/// unique, plusieurs documents de types différents peuvent le partager. F_DOCLIGNE porte
/// aussi DO_Type en clé dénormalisée (index IDL_LIGNE), d'où la mise à jour sur les deux
/// tables.
/// </summary>
public sealed class DocumentRepository
{
    public async Task<List<DocumentCandidate>> FindByPieceAsync(string piece)
    {
        const string sql = @"
            SELECT DO_Type, DO_Piece, DO_Domaine, DO_Valide
            FROM dbo.F_DOCENTETE
            WHERE DO_Piece = @piece
            ORDER BY DO_Type";
        var table = await DatabaseManager.Instance.ExecuteQueryAsync(sql, new Dictionary<string, object?> { ["@piece"] = piece });
        return table.Rows.Cast<System.Data.DataRow>().Select(r => new DocumentCandidate
        {
            DoType = Convert.ToInt32(r["DO_Type"]),
            DoPiece = (string)r["DO_Piece"],
            DoDomaine = Convert.ToInt32(r["DO_Domaine"]),
            DoValide = Convert.ToInt32(r["DO_Valide"]) != 0
        }).ToList();
    }

    public async Task DevaliderAsync(DocumentCandidate candidate)
    {
        await DatabaseManager.Instance.RunInTransactionAsync(async (conn, tx) =>
        {
            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                "UPDATE dbo.F_DOCENTETE SET DO_Valide = 0, DO_Imprim = 0, DO_Type = 6 WHERE DO_Type = @type AND DO_Piece = @piece",
                new Dictionary<string, object?> { ["@type"] = candidate.DoType, ["@piece"] = candidate.DoPiece });

            await DatabaseManager.ExecuteInTransactionAsync(conn, tx,
                "UPDATE dbo.F_DOCLIGNE SET DO_Type = 6 WHERE DO_Type = @type AND DO_Piece = @piece",
                new Dictionary<string, object?> { ["@type"] = candidate.DoType, ["@piece"] = candidate.DoPiece });
        });
    }
}
