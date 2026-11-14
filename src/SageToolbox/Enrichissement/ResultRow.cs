namespace SageToolbox.Enrichissement;

/// <summary>Ligne du tableau de résultats : donnée Sage + résultat de rapprochement API.</summary>
public sealed class ResultRow
{
    public required TierRecord Tier { get; init; }

    public bool Confirmed { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.NonTrouve;

    public string? SiretApi { get; set; }
    public string? NomApi { get; set; }
    public string? AdresseApi { get; set; }
    public string? TvaApi { get; set; }

    /// <summary>Entreprise/établissement API retenus (données brutes), utilisés pour reconstruire l'adresse Sage à la mise à jour.</summary>
    public ApiCompanyResult? SelectedCompany { get; set; }
    public ApiEtablissement? SelectedEtablissement { get; set; }

    /// <summary>Corrigée manuellement via la fenêtre de sélection (affichage en gras + ✎).</summary>
    public bool ManuallyCorrected { get; set; }

    /// <summary>
    /// Vrai lorsque le statut "A vérifier" provient d'un SIRET Sage dont l'entreprise (SIREN) est
    /// effectivement radiée mais dont un établissement actif alternatif a été retrouvé (déménagement
    /// possible, ou incohérence de données) — l'info affichée (SiretApi/NomApi/AdresseApi) reste celle
    /// d'origine, les <see cref="Candidates"/> proposant l'alternative active.
    /// </summary>
    public bool OriginEntrepriseRadiee { get; set; }

    /// <summary>
    /// Vrai lorsque le statut "A vérifier" provient d'un SIRET Sage dont seul l'établissement (et non
    /// l'entreprise) est fermé — l'entreprise reste active, l'établissement a probablement déménagé.
    /// </summary>
    public bool OriginEtablissementFerme { get; set; }

    /// <summary>Candidats API disponibles pour ouvrir la fenêtre de sélection (double-clic).</summary>
    public List<ApiCandidate> Candidates { get; set; } = new();

    /// <summary>Applique un candidat choisi manuellement (fenêtre de sélection) : recalcule statut, coche, marque comme corrigée.</summary>
    public void ApplyCandidate(ApiCandidate candidate)
    {
        SiretApi = candidate.Siret;
        NomApi = candidate.NomComplet;
        AdresseApi = candidate.AdresseComplete;
        TvaApi = Utils.GetTva(candidate.Company);
        SelectedCompany = candidate.Company;
        SelectedEtablissement = candidate.Etablissement;

        bool entrepriseRadiee = !candidate.EntrepriseActive;
        bool etablissementFerme = !candidate.EtablissementOuvert;

        Status = entrepriseRadiee ? MatchStatus.Radie
            : etablissementFerme ? MatchStatus.AVerifier
            : MatchStatus.Trouve;

        Confirmed = true;
        ManuallyCorrected = true;
    }
}
