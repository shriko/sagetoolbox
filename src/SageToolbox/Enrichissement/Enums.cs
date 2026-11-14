namespace SageToolbox.Enrichissement;

/// <summary>Type de tiers Sage (colonne CT_Type).</summary>
public enum TierType
{
    Client = 0,
    Fournisseur = 1,
    Tous = -1,
}

/// <summary>État du rapprochement SIRET/SIREN pour un tiers.</summary>
public enum MatchStatus
{
    Valide,
    Trouve,
    Multiples,
    AVerifier,
    Radie,
    NonTrouve,
    Erreur,
    Ignore,
}

public static class MatchStatusExtensions
{
    public static string ToDisplayString(this MatchStatus status) => status switch
    {
        MatchStatus.Valide => "Valide",
        MatchStatus.Trouve => "Trouvé",
        MatchStatus.Multiples => "Multiples",
        MatchStatus.AVerifier => "A vérifier",
        MatchStatus.Radie => "Radié",
        MatchStatus.NonTrouve => "Non trouvé",
        MatchStatus.Erreur => "Erreur",
        MatchStatus.Ignore => "Ignoré",
        _ => status.ToString(),
    };

    /// <summary>Seul le statut Valide est confirmé automatiquement.</summary>
    public static bool IsAutoConfirmed(this MatchStatus status) => status == MatchStatus.Valide;

    /// <summary>Non trouvé / Erreur / Ignoré ne sont jamais cochables.</summary>
    public static bool IsCheckable(this MatchStatus status) =>
        status is not (MatchStatus.NonTrouve or MatchStatus.Erreur or MatchStatus.Ignore);
}
