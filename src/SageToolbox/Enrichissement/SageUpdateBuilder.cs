
namespace SageToolbox.Enrichissement;

/// <summary>
/// Construit, pour une ligne confirmée du tableau et les options cochées dans la fenêtre de mise à jour,
/// le dictionnaire des colonnes F_COMPTET à modifier ainsi que le détail avant/après pour l'audit.
/// </summary>
public static class SageUpdateBuilder
{
    public const int MaxLenIntitule = 69;
    public const int MaxLenAdresse = 35;
    public const int MaxLenComplement = 35;
    public const int MaxLenCodePostal = 9;
    public const int MaxLenVille = 35;
    public const int MaxLenIdentifiant = 25;

    public static (Dictionary<string, object?> Columns, Dictionary<string, (string? Before, string? After)> AuditChanges) Build(
        ResultRow row, UpdateOptions options)
    {
        var columns = new Dictionary<string, object?>();
        var audit = new Dictionary<string, (string?, string?)>();

        void Set(string column, string? before, string? after)
        {
            columns[column] = after;
            audit[column] = (before, after);
        }

        if (options.UpdateSiret)
        {
            if (row.Status == MatchStatus.NonTrouve)
            {
                // Cas particulier : SIRET non retrouvé par l'API -> on vide le champ CT_Siret existant.
                Set("CT_Siret", row.Tier.CT_Siret, null);
            }
            else if (!string.IsNullOrEmpty(row.SiretApi))
            {
                Set("CT_Siret", row.Tier.CT_Siret, row.SiretApi);
            }
        }

        if (options.UpdateNom && !string.IsNullOrEmpty(row.NomApi))
        {
            string? nom = Utils.TruncateField(row.NomApi, MaxLenIntitule, nameof(row.NomApi));
            Set("CT_Intitule", row.Tier.CT_Intitule, nom);
        }

        if (options.UpdateAdresse && row.SelectedEtablissement is not null)
        {
            string voie = Utils.FormatVoie(row.SelectedEtablissement);
            voie = Utils.ApplyCasing(voie, options.AdresseEnMajuscules) ?? voie;
            var (adresse, complement) = Utils.SplitAddress(voie, MaxLenAdresse);

            Set("CT_Adresse", row.Tier.CT_Adresse, adresse);
            Set("CT_Complement", row.Tier.CT_Complement, string.IsNullOrEmpty(complement) ? null : complement);

            string? cp = Utils.TruncateField(row.SelectedEtablissement.CodePostal, MaxLenCodePostal, "CT_CodePostal");
            Set("CT_CodePostal", row.Tier.CT_CodePostal, cp);

            string? ville = row.SelectedEtablissement.LibelleCommune;
            ville = Utils.ApplyCasing(ville, options.AdresseEnMajuscules);
            ville = Utils.TruncateField(ville, MaxLenVille, "CT_Ville");
            Set("CT_Ville", row.Tier.CT_Ville, ville);
        }

        if (options.UpdateTva && !string.IsNullOrEmpty(row.TvaApi))
        {
            string? tva = Utils.TruncateField(row.TvaApi, MaxLenIdentifiant, nameof(row.TvaApi));
            Set("CT_Identifiant", row.Tier.CT_Identifiant, tva);
        }

        if (options.MettreEnSommeilSiRadie && row.Status == MatchStatus.Radie)
        {
            columns["CT_Sommeil"] = (short)1;
            audit["CT_Sommeil"] = (row.Tier.CT_Sommeil ? "1" : "0", "1");
        }

        return (columns, audit);
    }
}
