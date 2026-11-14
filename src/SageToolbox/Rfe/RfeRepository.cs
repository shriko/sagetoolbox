using System.Data;
using SageToolbox.Core;

namespace SageToolbox.Rfe;

/// <summary>
/// Listing des adresses de facturation électronique (« maillage » : F_COMPTETFACTRECEPTION, Sage 100 v12) des clients
/// actifs, avec leurs contacts (F_CONTACTT). Lecture seule.
/// </summary>
public sealed class RfeRepository
{
    private const string AddressesSql = @"
SELECT
    F.CT_NUM as Compte,
    CT_Intitule as [Raison Sociale],
    CT_Telephone as [Téléphone],
    CT_EMail [E-mail],
    CASE CFAR_CodeRoutageType
        WHEN 1 THEN 'SIREN'
        WHEN 2 THEN 'SIRET'
        WHEN 3 THEN 'SIRET_CODEROUTAGE'
        WHEN 4 THEN 'SIREN_SUFFIXE'
        ELSE 'Inconnu'
    END AS [Type Maille],
    CFAR_CodeRoutageVal as [Maille Adresse],
    CASE CFAR_Actif WHEN 0 THEN 'Non' WHEN 1 THEN 'Oui' END AS Actif,
    COUNT(*) OVER (PARTITION BY F.CT_NUM) AS [Nb Adresses]
FROM F_COMPTETFACTRECEPTION F LEFT JOIN F_COMPTET C on F.CT_NUM = C.CT_NUM
WHERE CT_SOMMEIL = 0 and CT_Type = 0
ORDER BY F.CT_NUM;
";

    private const string ContactsSql = @"
SELECT C.CT_NUM as Compte,
       N_Service as [Service],
       CT_NOM+' '+CT_Prenom as Contact,
       CT_Fonction as Fonction,
       C.CT_Telephone as Téléphone,
       C.CT_EMail as [E-mail]
FROM F_CONTACTT C LEFT JOIN F_COMPTET CT on C.CT_NUM = CT.CT_NUM
WHERE CT_TYPE = 0 and CT_SOMMEIL = 0
";

    private static string Text(DataRow row, string column) => row[column] is DBNull ? "" : row[column].ToString()!.Trim();

    /// <summary>Charge les adresses des clients actifs, chacune avec la liste des contacts de son compte.</summary>
    public async Task<List<RfeAddress>> LoadAsync()
    {
        var addressesTask = DatabaseManager.Instance.ExecuteQueryAsync(AddressesSql);
        var contactsTask = DatabaseManager.Instance.ExecuteQueryAsync(ContactsSql);
        await Task.WhenAll(addressesTask, contactsTask);

        var contactsByCompte = contactsTask.Result.Rows.Cast<DataRow>()
            .Select(r => new RfeContact
            {
                Compte = Text(r, "Compte"),
                Service = Text(r, "Service"),
                Contact = Text(r, "Contact"),
                Fonction = Text(r, "Fonction"),
                Telephone = Text(r, "Téléphone"),
                Email = Text(r, "E-mail"),
            })
            .GroupBy(c => c.Compte)
            .ToDictionary(g => g.Key, g => g.ToList());

        return addressesTask.Result.Rows.Cast<DataRow>()
            .Select(r =>
            {
                var compte = Text(r, "Compte");
                return new RfeAddress
                {
                    Compte = compte,
                    RaisonSociale = Text(r, "Raison Sociale"),
                    Telephone = Text(r, "Téléphone"),
                    Email = Text(r, "E-mail"),
                    TypeMaille = Text(r, "Type Maille"),
                    MailleAdresse = Text(r, "Maille Adresse"),
                    Actif = Text(r, "Actif"),
                    NbAdresses = Convert.ToInt32(r["Nb Adresses"]),
                    Contacts = contactsByCompte.GetValueOrDefault(compte) ?? new List<RfeContact>(),
                };
            })
            .ToList();
    }
}
