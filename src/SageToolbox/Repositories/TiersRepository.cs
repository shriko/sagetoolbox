using SageToolbox.Core;

namespace SageToolbox.Repositories;

/// <summary>
/// Correction des adresses de livraison manquantes (#14). Portage fidèle de
/// correction_adresse_gescom (AIG_Toolbox.py V1) : F_COMPTET.CT_Type = 0 identifie un
/// tiers client (confirme la colonne de classification que le PDF ne documentait pas),
/// LI_No est un compteur géré applicativement (MAX(LI_No)+1 en une seule requête via
/// ROW_NUMBER, pas une colonne IDENTITY SQL Server).
///
/// Léger écart volontaire par rapport à V1 sur le rattachement des documents orphelins :
/// V1 fait un UPDATE ... FROM ... INNER JOIN sans filtrer sur LI_Principal, ce qui peut
/// être non déterministe si un tiers a plusieurs lignes F_LIVRAISON (jointure en éventail).
/// Ici on restreint explicitement au lieu de livraison principal (LI_Principal = 1),
/// strictement équivalent quand il n'y en a qu'un — cas normal après la création ci-dessus.
/// </summary>
public sealed class TiersRepository
{
    public async Task<int> CorrigerAdressesLivraisonAsync()
    {
        var maxLiNoResult = await DatabaseManager.Instance.ExecuteScalarAsync(
            "SELECT ISNULL(MAX(LI_No), 0) FROM dbo.F_LIVRAISON");
        var maxLiNo = Convert.ToInt32(maxLiNoResult);

        const string insertSql = @"
            INSERT INTO dbo.F_LIVRAISON
                (LI_No, CT_Num, LI_Intitule, LI_Adresse, LI_Complement, LI_CodePostal, LI_Ville,
                 LI_CodeRegion, LI_Pays, LI_Contact, N_Expedition, N_Condition, LI_Telephone,
                 LI_Telecopie, LI_EMail, LI_DelaiTransport, LI_Commentaire, LI_Principal, LI_AdresseFact)
            SELECT
                @lino + ROW_NUMBER() OVER (ORDER BY CT_Num ASC),
                CT_Num,
                LEFT(CT_Intitule, 69), LEFT(ISNULL(CT_Adresse, ''), 35), LEFT(ISNULL(CT_Complement, ''), 35),
                LEFT(ISNULL(CT_CodePostal, ''), 9), LEFT(ISNULL(CT_Ville, ''), 35), LEFT(ISNULL(CT_CodeRegion, ''), 25),
                LEFT(ISNULL(CT_Pays, ''), 35), LEFT(ISNULL(CT_Contact, ''), 35), N_Expedition, N_Condition,
                LEFT(ISNULL(CT_Telephone, ''), 21), LEFT(ISNULL(CT_Telecopie, ''), 21), LEFT(ISNULL(CT_EMail, ''), 69),
                ISNULL(CT_DelaiTransport, 0), CT_Commentaire, 1, 0
            FROM dbo.F_COMPTET
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.F_LIVRAISON AS fl WHERE fl.CT_Num = F_COMPTET.CT_Num AND fl.LI_Principal = 1
            ) AND CT_Type = 0;";

        var created = await DatabaseManager.Instance.ExecuteNonQueryAsync(insertSql,
            new Dictionary<string, object?> { ["@lino"] = maxLiNo });

        const string updateSql = @"
            UPDATE dbo.F_DOCENTETE
            SET LI_No = fl.LI_No
            FROM dbo.F_DOCENTETE de
            INNER JOIN dbo.F_LIVRAISON fl ON de.DO_Tiers = fl.CT_Num AND fl.LI_Principal = 1
            WHERE de.LI_No = 0;";
        await DatabaseManager.Instance.ExecuteNonQueryAsync(updateSql);

        return created;
    }
}
