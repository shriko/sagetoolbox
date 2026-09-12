namespace SageToolbox.Models;

/// <summary>
/// Une entête F_DOCENTETE candidate pour la dévalidation (#12). DO_Piece n'est pas
/// unique à lui seul dans Sage (clé réelle = DO_Type + DO_Piece, index IDO_TPIECE) :
/// plusieurs documents de types différents peuvent partager le même numéro de pièce.
/// </summary>
public sealed class DocumentCandidate
{
    public int DoType { get; init; }
    public string DoPiece { get; init; } = string.Empty;
    public int DoDomaine { get; init; }
    public bool DoValide { get; init; }

    public string DomaineLabel => DoDomaine switch
    {
        0 => "Vente",
        1 => "Achat",
        2 => "Stock",
        3 => "Ticket",
        4 => "Document interne",
        _ => $"Domaine {DoDomaine}"
    };

    public string Libelle => $"Pièce {DoPiece} — type {DoType} ({DomaineLabel}) — {(DoValide ? "Validée" : "Brouillon")}";
}
