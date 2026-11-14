using System.Text.Json.Serialization;

namespace SageToolbox.Enrichissement;

/// <summary>Enveloppe de réponse de l'API "Recherche d'entreprises".</summary>
public sealed class ApiSearchResponse
{
    [JsonPropertyName("results")]
    public List<ApiCompanyResult> Results { get; set; } = new();
}

public sealed class ApiCompanyResult
{
    [JsonPropertyName("nom_complet")]
    public string? NomComplet { get; set; }

    [JsonPropertyName("siren")]
    public string? Siren { get; set; }

    [JsonPropertyName("siret")]
    public string? Siret { get; set; }

    /// <summary>A = actif, C = cessée (au niveau entreprise/SIREN).</summary>
    [JsonPropertyName("etat_administratif")]
    public string? EtatAdministratif { get; set; }

    [JsonPropertyName("matching_etablissements")]
    public List<ApiEtablissement>? MatchingEtablissements { get; set; }

    [JsonPropertyName("siege")]
    public ApiEtablissement? Siege { get; set; }

    [JsonPropertyName("complements")]
    public ApiComplements? Complements { get; set; }

    public bool EstActive => string.Equals(EtatAdministratif, "A", StringComparison.OrdinalIgnoreCase);
}

public sealed class ApiEtablissement
{
    [JsonPropertyName("siret")]
    public string? Siret { get; set; }

    [JsonPropertyName("etat_administratif")]
    public string? EtatAdministratif { get; set; }

    [JsonPropertyName("numero_voie")]
    public string? NumeroVoie { get; set; }

    [JsonPropertyName("type_voie")]
    public string? TypeVoie { get; set; }

    [JsonPropertyName("libelle_voie")]
    public string? LibelleVoie { get; set; }

    [JsonPropertyName("complement_adresse")]
    public string? ComplementAdresse { get; set; }

    [JsonPropertyName("code_postal")]
    public string? CodePostal { get; set; }

    [JsonPropertyName("libelle_commune")]
    public string? LibelleCommune { get; set; }

    /// <summary>Niveau établissement : A = ouvert, F = fermé.</summary>
    public bool EstOuvert => string.Equals(EtatAdministratif, "A", StringComparison.OrdinalIgnoreCase);
    public bool EstFerme => !EstOuvert;
}

public sealed class ApiComplements
{
    [JsonPropertyName("tva_intracommunautaire")]
    public string? TvaIntracommunautaire { get; set; }
}
