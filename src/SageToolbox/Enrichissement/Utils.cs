using System.Text;
using System.Text.RegularExpressions;
using SageToolbox.Core;

namespace SageToolbox.Enrichissement;

/// <summary>Fonctions pures et testables utilisées par le moteur de rapprochement et la mise à jour Sage.</summary>
public static partial class Utils
{
    private static readonly Regex SiretRegex = new("^[0-9]{14}$", RegexOptions.Compiled);
    private static readonly Regex SirenRegex = new("^[0-9]{9}$", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNumRegex = new(@"[^\p{L}\p{Nd} ]", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Formes juridiques supprimées en préfixe ou suffixe du nom nettoyé.</summary>
    private static readonly string[] LegalForms =
    {
        "ASSOCIATION", "EURL", "SARL", "SAS", "SA", "SNC", "SCI",
    };

    public static bool IsValidSiret(string? siret) => siret is not null && SiretRegex.IsMatch(siret);

    public static bool IsValidSiren(string? siren) => siren is not null && SirenRegex.IsMatch(siren);

    /// <summary>Majuscules, ponctuation supprimée, forme juridique retirée en préfixe/suffixe, espaces compactés.</summary>
    public static string CleanCompanyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        string cleaned = name.ToUpperInvariant();
        cleaned = cleaned.Replace("'", " ").Replace("-", " ");
        cleaned = NonAlphaNumRegex.Replace(cleaned, " ");
        cleaned = MultiSpaceRegex.Replace(cleaned, " ").Trim();

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count > 1)
        {
            if (LegalForms.Contains(words[0])) words.RemoveAt(0);
            else if (words.Count > 1 && LegalForms.Contains(words[^1])) words.RemoveAt(words.Count - 1);
        }

        return string.Join(' ', words);
    }

    /// <summary>Meilleur SIRET représentatif d'un résultat API (siège en priorité, sinon 1er établissement).</summary>
    public static string? GetBestSiret(ApiCompanyResult company)
    {
        if (!string.IsNullOrEmpty(company.Siege?.Siret)) return company.Siege!.Siret;
        if (!string.IsNullOrEmpty(company.Siret)) return company.Siret;
        return company.MatchingEtablissements?.FirstOrDefault()?.Siret;
    }

    public static string? GetBestCandidateName(ApiCompanyResult company) => company.NomComplet;

    /// <summary>Tronque un champ à la longueur max, journalise un avertissement en cas de troncature.</summary>
    public static string? TruncateField(string? value, int maxLength, string? fieldNameForLog = null)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;

        if (fieldNameForLog is not null)
        {
            Logger.Instance.Warning($"Enrichissement tiers : troncature du champ {fieldNameForLog} : {value.Length} -> {maxLength} caractères.");
        }

        return value[..maxLength];
    }

    /// <summary>Casse "Title Case" ou majuscules.</summary>
    public static string? ApplyCasing(string? value, bool upper)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return upper ? value.ToUpperInvariant() : System.Globalization.CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(value.ToLowerInvariant());
    }

    /// <summary>Voie reconstruite depuis les champs de l'établissement API (base du découpage CT_Adresse/CT_Complement).</summary>
    public static string FormatVoie(ApiEtablissement etablissement)
    {
        var parts = new[]
        {
            etablissement.NumeroVoie,
            etablissement.TypeVoie,
            etablissement.LibelleVoie,
            etablissement.ComplementAdresse,
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(' ', parts);
    }

    /// <summary>Adresse complète (voie + code postal + commune) pour affichage (colonne "Adresse API", dialogues).</summary>
    public static string FormatFullAddress(ApiEtablissement etablissement)
    {
        string voie = FormatVoie(etablissement);
        var parts = new[] { voie, etablissement.CodePostal, etablissement.LibelleCommune }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Découpe une adresse complète en (CT_Adresse, CT_Complement), chacun limité à <paramref name="maxLength"/>
    /// caractères, en cherchant un point de coupure propre (espace/virgule/tiret) ; troncature dure en dernier recours.
    /// </summary>
    public static (string Adresse, string Complement) SplitAddress(string fullAddress, int maxLength = 35)
    {
        fullAddress = fullAddress.Trim();
        if (fullAddress.Length <= maxLength) return (fullAddress, string.Empty);

        int cut = FindCleanBreakPoint(fullAddress, maxLength);
        string adresse = fullAddress[..cut].TrimEnd(' ', ',', '-');
        string rest = fullAddress[cut..].TrimStart(' ', ',', '-');

        if (rest.Length > maxLength)
        {
            Logger.Instance.Warning($"Enrichissement tiers : troncature dure du complément d'adresse : {rest.Length} -> {maxLength} caractères.");
            rest = rest[..maxLength];
        }

        return (adresse, rest);
    }

    private static int FindCleanBreakPoint(string text, int maxLength)
    {
        for (int i = Math.Min(maxLength, text.Length - 1); i > 0; i--)
        {
            if (text[i] is ' ' or ',' or '-') return i;
        }
        return maxLength;
    }

    /// <summary>
    /// N° TVA intracommunautaire : celui fourni par l'API sinon calculé depuis le SIREN
    /// (clé = (12 + 3 × (SIREN mod 97)) mod 97, format FRxx + SIREN).
    /// </summary>
    public static string? GetTva(ApiCompanyResult company)
    {
        if (!string.IsNullOrWhiteSpace(company.Complements?.TvaIntracommunautaire))
        {
            return company.Complements.TvaIntracommunautaire;
        }

        string? siren = company.Siren;
        if (!IsValidSiren(siren)) return null;

        long sirenValue = long.Parse(siren!);
        long key = (12 + 3 * (sirenValue % 97)) % 97;
        return $"FR{key:D2}{siren}";
    }
}
