namespace SageToolbox.Enrichissement;

/// <summary>
/// Candidat aplati (entreprise + un établissement) tel que présenté dans la fenêtre de sélection :
/// nom complet, SIRET, statut actif/radié, adresse.
/// </summary>
public sealed class ApiCandidate
{
    public required ApiCompanyResult Company { get; init; }
    public required ApiEtablissement Etablissement { get; init; }

    public string NomComplet => Company.NomComplet ?? string.Empty;
    public string? Siret => Etablissement.Siret ?? Company.Siret;
    public bool EntrepriseActive => Company.EstActive;
    public bool EtablissementOuvert => Etablissement.EstOuvert;
    public string AdresseComplete => Utils.FormatFullAddress(Etablissement);
}
