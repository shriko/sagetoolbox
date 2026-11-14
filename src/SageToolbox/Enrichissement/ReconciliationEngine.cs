using SageToolbox.Core;

namespace SageToolbox.Enrichissement;

/// <summary>
/// État-machine du rapprochement SIRET/SIREN (cœur métier, indépendant de l'UI et de la persistance).
/// Pour chaque tiers : vérifie le SIRET Sage existant, ou recherche par SIREN/nom+CP si absent/invalide.
/// </summary>
public sealed class ReconciliationEngine
{
    private readonly IApiClient _api;

    public ReconciliationEngine(IApiClient api)
    {
        _api = api;
    }

    public async Task<ResultRow> ProcessTierAsync(TierRecord tier, CancellationToken ct)
    {
        var row = new ResultRow { Tier = tier };

        try
        {
            if (Utils.IsValidSiret(tier.CT_Siret))
            {
                await ProcessWithExistingSiretAsync(row, tier.CT_Siret!, ct).ConfigureAwait(false);
            }
            else
            {
                await ProcessWithoutValidSiretAsync(row, tier, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Enrichissement tiers : erreur lors du rapprochement du tiers {tier.CT_Num} — {ex.Message}");
            row.Status = MatchStatus.Erreur;
        }

        return row;
    }

    /// <summary>1. Le SIRET Sage est valide (14 chiffres) : vérification.</summary>
    private async Task ProcessWithExistingSiretAsync(ResultRow row, string siret, CancellationToken ct)
    {
        var results = await _api.SearchBySiretAsync(siret, ct).ConfigureAwait(false);
        var match = FindExactMatch(results, siret);

        if (match is not null)
        {
            var (company, etablissement) = match.Value;
            bool entrepriseRadiee = !company.EstActive;
            bool etablissementFerme = etablissement.EstFerme;

            SetApiFields(row, company, etablissement);

            if (etablissementFerme || entrepriseRadiee)
            {
                // Établissement fermé et/ou entreprise radiée : recherche complémentaire par SIREN pour
                // proposer un établissement actif alternatif (ex. déménagement). Seuls les candidats de
                // cette recherche font foi pour détecter une alternative active — jamais l'établissement
                // d'origine lui-même, qui serait sinon toujours considéré comme sa propre "alternative".
                // L'info d'origine (fermée/radiée) reste affichée dans les colonnes API.
                var sirenResults = await _api.SearchBySirenAsync(company.Siren ?? siret[..9], ct).ConfigureAwait(false);
                var candidates = FlattenCandidates(sirenResults);
                bool hasActiveAlternative = candidates.Any(c => c.EtablissementOuvert);

                if (!entrepriseRadiee)
                {
                    // Entreprise confirmée active : seul l'établissement pointé est fermé, l'utilisateur
                    // doit choisir/valider le bon établissement (déménagement probable), même si la
                    // recherche par SIREN ne propose encore aucune alternative automatiquement.
                    row.Status = MatchStatus.AVerifier;
                    row.Candidates = candidates;
                    row.OriginEtablissementFerme = true;
                }
                else if (hasActiveAlternative)
                {
                    // Entreprise radiée en apparence, mais un établissement actif existe bien sous le
                    // même SIREN (déménagement ou incohérence de données) : ne pas conclure à tort à une
                    // radiation définitive.
                    row.Status = MatchStatus.AVerifier;
                    row.Candidates = candidates;
                    row.OriginEntrepriseRadiee = true;
                }
                else
                {
                    row.Status = MatchStatus.Radie;
                }
            }
            else
            {
                row.Status = MatchStatus.Valide;
                row.Confirmed = true;
            }

            return;
        }

        // SIRET non trouvé ou ne correspond pas : repli sur une recherche par SIREN.
        string siren = siret[..9];
        var bysiren = await _api.SearchBySirenAsync(siren, ct).ConfigureAwait(false);

        if (bysiren.Count > 0)
        {
            row.Status = MatchStatus.AVerifier;
            row.Candidates = FlattenCandidates(bysiren);
            var best = row.Candidates.FirstOrDefault();
            if (best is not null) SetApiFields(row, best.Company, best.Etablissement);
        }
        else
        {
            row.Status = MatchStatus.NonTrouve;
        }
    }

    /// <summary>2. Le SIRET Sage est absent/invalide : recherche.</summary>
    private async Task ProcessWithoutValidSiretAsync(ResultRow row, TierRecord tier, CancellationToken ct)
    {
        List<ApiCompanyResult> results;

        if (Utils.IsValidSiren(tier.CT_Siret))
        {
            results = await _api.SearchBySirenAsync(tier.CT_Siret!, ct).ConfigureAwait(false);
        }
        else
        {
            string cleanedName = Utils.CleanCompanyName(tier.CT_Intitule);
            if (cleanedName.Length < 3)
            {
                row.Status = MatchStatus.NonTrouve;
                return;
            }

            string? postalCode = tier.CT_CodePostal?.Length >= 5 ? tier.CT_CodePostal : null;
            results = await _api.SearchByNameAsync(cleanedName, postalCode, ct).ConfigureAwait(false);
        }

        results = SortResults(results);

        if (results.Count == 0)
        {
            row.Status = MatchStatus.NonTrouve;
            return;
        }

        if (results.Count > 1)
        {
            row.Status = MatchStatus.Multiples;
            row.Candidates = FlattenCandidates(results);
            var firstCandidate = row.Candidates.FirstOrDefault();
            if (firstCandidate is not null) SetApiFields(row, firstCandidate.Company, firstCandidate.Etablissement);
            return;
        }

        var company = results[0];
        var etablissement = GetPrimaryEtablissement(company);
        if (etablissement is null)
        {
            row.Status = MatchStatus.NonTrouve;
            return;
        }

        SetApiFields(row, company, etablissement);
        row.Candidates = FlattenCandidates(results);

        if (!company.EstActive)
        {
            row.Status = MatchStatus.Radie;
        }
        else if (etablissement.EstFerme)
        {
            row.Status = MatchStatus.AVerifier;
        }
        else
        {
            row.Status = MatchStatus.Trouve; // Nécessite confirmation manuelle, pas auto-coché.
        }
    }

    private static void SetApiFields(ResultRow row, ApiCompanyResult company, ApiEtablissement etablissement)
    {
        row.SiretApi = etablissement.Siret ?? company.Siret;
        row.NomApi = company.NomComplet;
        row.AdresseApi = Utils.FormatFullAddress(etablissement);
        row.TvaApi = Utils.GetTva(company);
        row.SelectedCompany = company;
        row.SelectedEtablissement = etablissement;
    }

    private static (ApiCompanyResult Company, ApiEtablissement Etablissement)? FindExactMatch(List<ApiCompanyResult> results, string siret)
    {
        foreach (var company in results)
        {
            if (string.Equals(company.Siege?.Siret, siret, StringComparison.Ordinal))
            {
                return (company, company.Siege!);
            }

            var match = company.MatchingEtablissements?.FirstOrDefault(e => string.Equals(e.Siret, siret, StringComparison.Ordinal));
            if (match is not null) return (company, match);
        }

        return null;
    }

    private static ApiEtablissement? GetPrimaryEtablissement(ApiCompanyResult company) =>
        company.Siege ?? company.MatchingEtablissements?.FirstOrDefault();

    /// <summary>Entreprises actives d'abord, puis établissements actifs en premier au sein de chaque entreprise.</summary>
    private static List<ApiCompanyResult> SortResults(List<ApiCompanyResult> results) =>
        results
            .OrderByDescending(c => c.EstActive)
            .ThenByDescending(c => GetPrimaryEtablissement(c)?.EstOuvert ?? false)
            .ToList();

    private static List<ApiCandidate> FlattenCandidates(List<ApiCompanyResult> results)
    {
        var candidates = new List<ApiCandidate>();

        foreach (var company in results)
        {
            var etablissements = new List<ApiEtablissement>();
            if (company.Siege is not null) etablissements.Add(company.Siege);
            if (company.MatchingEtablissements is not null)
            {
                etablissements.AddRange(company.MatchingEtablissements.Where(e =>
                    company.Siege is null || !string.Equals(e.Siret, company.Siege.Siret, StringComparison.Ordinal)));
            }

            foreach (var etab in etablissements.OrderByDescending(e => e.EstOuvert))
            {
                candidates.Add(new ApiCandidate { Company = company, Etablissement = etab });
            }
        }

        return candidates;
    }
}
