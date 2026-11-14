namespace SageToolbox.Enrichissement;

/// <summary>Fiche tiers issue de F_COMPTET (Sage 100).</summary>
public sealed class TierRecord
{
    public required string CT_Num { get; init; }
    public string? CT_Intitule { get; init; }
    public string? CT_Adresse { get; init; }
    public string? CT_Complement { get; init; }
    public string? CT_CodePostal { get; init; }
    public string? CT_Ville { get; init; }
    public string? CT_Siret { get; init; }
    public TierType CT_Type { get; init; }
    public string? CT_Identifiant { get; init; }
    public bool CT_Sommeil { get; init; }
}
