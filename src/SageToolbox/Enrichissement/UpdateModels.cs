namespace SageToolbox.Enrichissement;

/// <summary>Options cochées dans la fenêtre « Mettre à jour Sage 100 ».</summary>
public sealed class UpdateOptions
{
    /// <summary>Toujours vrai : le SIRET est la donnée pivot du rapprochement.</summary>
    public bool UpdateSiret { get; init; } = true;

    public bool UpdateNom { get; init; }
    public bool UpdateAdresse { get; init; }
    public bool AdresseEnMajuscules { get; init; }
    public bool UpdateTva { get; init; }
    public bool MettreEnSommeilSiRadie { get; init; }
}

/// <summary>Résultat de l'application d'une mise à jour Sage pour une ligne.</summary>
public sealed record UpdateResult(string CtNum, bool Success, string? ErrorMessage, Dictionary<string, (string? Before, string? After)> Changes);
