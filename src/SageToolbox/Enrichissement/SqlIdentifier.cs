using System.Text.RegularExpressions;

namespace SageToolbox.Enrichissement;

/// <summary>
/// Validation anti-injection des identifiants SQL dynamiques (nom de schéma multi-société,
/// noms de colonnes à mettre à jour) : liste blanche stricte, jamais construits depuis une
/// entrée libre non validée.
/// </summary>
public static partial class SqlIdentifier
{
    private static readonly Regex ValidIdentifier = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static bool IsValid(string identifier) => !string.IsNullOrEmpty(identifier) && ValidIdentifier.IsMatch(identifier);

    /// <summary>Retourne l'identifiant entre crochets s'il est valide, lève une exception sinon.</summary>
    public static string Bracket(string identifier)
    {
        if (!IsValid(identifier))
        {
            throw new ArgumentException($"Identifiant SQL invalide (caractères non autorisés) : '{identifier}'", nameof(identifier));
        }

        return $"[{identifier}]";
    }
}
