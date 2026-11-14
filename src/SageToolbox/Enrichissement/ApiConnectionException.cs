namespace SageToolbox.Enrichissement;

/// <summary>
/// Levée quand l'API "Recherche d'entreprises" n'a pas pu être contactée ou n'a pas répondu
/// correctement (timeout de connexion, erreur réseau, code HTTP d'erreur, 429 répétés).
/// Distincte d'une réponse HTTP 200 avec zéro résultat (véritable "non trouvé") : ce cas signale
/// une incertitude, pas une absence confirmée dans le registre — le tiers doit être marqué
/// "Erreur" (à retraiter) plutôt que "Non trouvé" pour éviter, par exemple, qu'une mise à jour Sage
/// n'efface à tort un SIRET valide suite à un simple incident réseau.
/// </summary>
public sealed class ApiConnectionException : Exception
{
    public ApiConnectionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
