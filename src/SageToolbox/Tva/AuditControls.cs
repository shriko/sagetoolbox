
namespace SageToolbox.Tva;

/// <summary>
/// Contrôles d'audit TVA exécutés sur chaque base Sage 100 Comptabilité.
/// 4.2 à 4.7 reprennent les scripts historiques ; 4.8 à 4.11 sont les nouveaux contrôles d'anomalie.
/// (L'ancien 4.1, inventaire des bases, est remplacé par l'écran de sélection.)
/// </summary>
public static class AuditControls
{
    // Comptes du paramétrage TVA (P_FISCAL) : exclus des contrôles « compte de TVA sans code taxe ».
    private const string ComptesParametrage = """
        SELECT ISNULL(CG_NumMAJDecaisser, '') FROM P_FISCAL UNION SELECT ISNULL(CG_NumMAJCredit, '') FROM P_FISCAL
        UNION SELECT ISNULL(CG_NumMAJEcartD, '') FROM P_FISCAL UNION SELECT ISNULL(CG_NumMAJEcartC, '') FROM P_FISCAL
        """;

    private static string ControleCompte(int ordre, string libelle, string colonne, string commentaireAbsent) => $"""
        SELECT {ordre} AS Ord, N'{libelle.Replace("'", "''")}' AS [Contrôle], ISNULL(p.{colonne}, '') AS [Valeur],
               CASE WHEN ISNULL(p.{colonne}, '') = '' THEN N'{commentaireAbsent.Replace("'", "''")}'
                    WHEN NOT EXISTS (SELECT 1 FROM F_COMPTEG g WHERE g.CG_Num = p.{colonne}) THEN N'Compte inexistant'
                    ELSE '' END AS [Anomalie]
        FROM p
        """;

    public static IReadOnlyList<ControlDefinition> All { get; } =
    [
        new("4.2", "Paramétrage société",
            "Options de la société et paramétrage fiscal de la TVA.",
            """
            SELECT  d.D_RAISONSOC AS [Société],
                    CASE d.D_ProtPiece WHEN 0 THEN 'Non' WHEN 1 THEN 'Oui' END AS [Protection pièce],
                    CASE d.D_NumCont WHEN 0 THEN 'Non' WHEN 1 THEN 'Oui' END AS [Numérotation continue],
                    CASE WHEN ISNULL(d.JO_NumAN, '') = '' AND ISNULL(d.CG_NUMANOUV, '') = ''
                              AND ISNULL(d.CG_NUMANBENEF, '') = '' AND ISNULL(d.CG_NUMANPerte, '') = '' THEN 'Non'
                         WHEN ISNULL(d.JO_NumAN, '') <> '' AND ISNULL(d.CG_NUMANOUV, '') <> ''
                              AND ISNULL(d.CG_NUMANBENEF, '') <> '' AND ISNULL(d.CG_NUMANPerte, '') <> '' THEN 'Oui'
                         ELSE 'Partiellement' END AS [Paramètre AN renseigné ?],
                    d.BM_Intitule AS [Nom du param. bcr],
                    CASE CAST(f.F_REGIME AS varchar(5))
                         WHEN '0' THEN 'CA3 - Réel normal'
                         WHEN '1' THEN 'CA3 - Mini réel'
                         WHEN '2' THEN 'CA12/CA12E - Réel simplifié'
                         WHEN '3' THEN 'CA3 - Réel simplifié agriculture' END AS [Régime fiscal],
                    f.JO_NumMAJ AS [Journal OD TVA],
                    f.CG_NumMAJDecaisser AS [Compte TVA à décaisser],
                    f.CG_NumMAJCredit AS [Compte crédit de TVA],
                    f.CG_NumMAJEcartD AS [Écart TVA débit],
                    f.CG_NumMAJEcartC AS [Écart TVA crédit],
                    CASE r.R_Registre02 WHEN 0 THEN 'Non' WHEN 1 THEN 'Oui' END AS [Gestion des registres de taxe],
                    CASE d.D_TvaEncReg WHEN 0 THEN 'Non' WHEN 1 THEN 'Oui' END AS [Encaissement sur registre ?]
            FROM P_DOSSIER d
            CROSS JOIN P_FISCAL f
            CROSS JOIN P_REGISTRE r
            """,
            Transposed: true),

        new("4.3", "Liste des journaux",
            "Journaux actifs, mode de numérotation et dernière utilisation.",
            """
            SELECT  CASE j.JO_TYPE WHEN 0 THEN 'Achats' WHEN 1 THEN 'Ventes' WHEN 2 THEN 'Trésorerie'
                                   WHEN 3 THEN 'Général' WHEN 4 THEN 'Situation' END AS [Type],
                    j.JO_NUM AS [Journal],
                    j.JO_INTITULE AS [Intitulé],
                    CASE j.JO_Numpiece WHEN 0 THEN 'Manuelle' WHEN 1 THEN 'Continue par journal'
                                       WHEN 2 THEN 'Continue pour la base' WHEN 3 THEN 'Mensuelle' END AS [N° pièce],
                    ISNULL(j.CG_NUM, '') AS [Compte trésorerie],
                    CASE WHEN MAX(e.JM_Date) IS NULL THEN 'N/A' ELSE FORMAT(MAX(e.JM_Date), 'MM/yyyy') END AS [Dernière utilisation]
            FROM F_JOURNAUX j
            LEFT JOIN F_ECRITUREC e ON e.JO_NUM = j.JO_NUM
            WHERE j.JO_Sommeil = 0
            GROUP BY j.JO_NUM, j.JO_INTITULE, j.JO_TYPE, j.JO_Numpiece, j.CG_NUM
            ORDER BY j.JO_NUM
            """),

        new("4.4", "Liste des comptes de TVA",
            "Comptes 4452, 4456 et 4457 et leur dernière utilisation.",
            """
            SELECT  c.CG_NUM AS [N° compte],
                    c.CG_Intitule AS [Libellé compte],
                    CASE WHEN MAX(e.JM_Date) IS NULL THEN 'N/A' ELSE FORMAT(MAX(e.JM_Date), 'MM/yyyy') END AS [Dernière utilisation]
            FROM F_COMPTEG c
            LEFT JOIN F_ECRITUREC e ON e.CG_NUM = c.CG_NUM
            WHERE c.N_Nature = 0 AND c.CG_Type = 0
              AND (c.CG_Num LIKE '4452%' OR c.CG_Num LIKE '4456%' OR c.CG_Num LIKE '4457%')
            GROUP BY c.CG_NUM, c.CG_Intitule
            ORDER BY c.CG_NUM
            """),

        new("4.5", "Liste des codes taxe",
            "Codes taxe de TVA ; un compte partagé entre plusieurs codes est signalé.",
            """
            SELECT  CASE t.TA_Sens WHEN 0 THEN '-' WHEN 1 THEN '+' END AS [Sens],
                    t.TA_Code AS [Code taxe],
                    t.TA_Regroup AS [Regroupement],
                    t.TA_Intitule AS [Intitulé taxe],
                    t.CG_Num AS [Compte taxe],
                    FORMAT(t.TA_Taux, '0.######') + '%' AS [Taux],
                    CASE t.TA_Type WHEN 0 THEN 'TVA/Débit' WHEN 1 THEN 'TVA/Encaissement' WHEN 5 THEN 'TVA/CEE'
                                   ELSE 'Autres taxes' END AS [Type de taxe],
                    CASE t.TA_Provenance WHEN 0 THEN 'Nationale' WHEN 1 THEN 'Intra-communautaire' WHEN 2 THEN 'Export'
                         WHEN 3 THEN 'Divers 1' WHEN 4 THEN 'Divers 2' WHEN 5 THEN 'Divers 3' WHEN 6 THEN 'Divers 4'
                         WHEN 7 THEN 'Divers 5' WHEN 8 THEN 'Îles Canaries' WHEN 9 THEN 'Ceuta et Melilla' END AS [Provenance],
                    FORMAT(t.TA_Assujet, '0.######') + '%' AS [Assujettissement],
                    t.TA_EdiCode AS [Code EDI],
                    CASE WHEN t.MEX_no = 0 THEN 'Non' ELSE 'Oui' END AS [Mention exo ?],
                    CASE WHEN a.CG_NUM IS NOT NULL THEN 'Compte partagé entre plusieurs codes taxe' ELSE '' END AS [Anomalie]
            FROM F_TAXE t
            LEFT JOIN (SELECT CG_NUM FROM F_TAXE GROUP BY CG_NUM HAVING COUNT(DISTINCT TA_CODE) > 1) a ON a.CG_NUM = t.CG_NUM
            WHERE t.TA_TTAUX = 0 AND t.TA_TYPE IN (0, 1, 5)
            ORDER BY t.TA_Regroup, t.TA_Code
            """,
            "Anomalie"),

        new("4.6", "Comptes rattachés à un code taxe",
            "Rattachements F_ETAXE : multi-affectation ou code taxe différent de celui du compte.",
            """
            SELECT  e.CG_NUM AS [N° compte],
                    c.CG_Intitule AS [Intitulé compte],
                    t.TA_CODE AS [Présent dans code taxe],
                    ISNULL(c.TA_CODE, '') AS [Code taxe associé sur compte],
                    CASE WHEN a.CG_NUM IS NOT NULL AND ISNULL(c.TA_CODE, '') <> ISNULL(t.TA_CODE, '') AND ISNULL(c.TA_CODE, '') <> ''
                              THEN 'Multi-affectation / code taxe différent'
                         WHEN a.CG_NUM IS NOT NULL THEN 'Multi-affectation'
                         WHEN ISNULL(c.TA_CODE, '') <> ISNULL(t.TA_CODE, '') AND ISNULL(c.TA_CODE, '') <> ''
                              THEN 'Code taxe différent'
                         ELSE '' END AS [Anomalie]
            FROM F_ETAXE e
            LEFT JOIN F_COMPTEG c ON c.CG_NUM = e.CG_NUM
            LEFT JOIN F_TAXE t ON t.TA_NO = e.TA_NO
            LEFT JOIN (SELECT CG_NUM FROM F_ETAXE GROUP BY CG_NUM HAVING COUNT(DISTINCT TA_NO) > 1) a ON a.CG_NUM = e.CG_NUM
            WHERE t.TA_TTAUX = 0 AND t.TA_TYPE IN (0, 1, 5)
            ORDER BY e.CG_NUM, e.TA_NO
            """,
            "Anomalie"),

        new("4.7", "Comptes généraux ayant un code taxe",
            "Comptes portant un code taxe mais absents des rattachements de ce code.",
            """
            SELECT  c.CG_NUM AS [N° compte],
                    c.CG_Intitule AS [Intitulé compte],
                    c.TA_CODE AS [Code taxe associé],
                    CASE WHEN NOT EXISTS (SELECT 1 FROM F_ETAXE e WHERE e.CG_NUM = c.CG_NUM)
                         THEN 'Compte non présent dans le code taxe' ELSE '' END AS [Anomalie]
            FROM F_COMPTEG c
            WHERE c.CG_TYPE = 0 AND ISNULL(c.TA_CODE, '') <> ''
            ORDER BY c.CG_NUM
            """,
            "Anomalie"),

        new("4.8", "Comptes de TVA sans code taxe",
            "Comptes 4452/4456/4457 ni portés par un code taxe, ni rattachés, ni déclarés dans le paramétrage fiscal.",
            $"""
            SELECT  c.CG_NUM AS [N° compte],
                    c.CG_Intitule AS [Intitulé compte],
                    ISNULL(c.TA_CODE, '') AS [Code taxe sur compte],
                    (SELECT COUNT(DISTINCT e.TA_NO) FROM F_ETAXE e WHERE e.CG_NUM = c.CG_NUM) AS [Nb rattachements],
                    (SELECT COUNT(*) FROM F_TAXE t WHERE t.CG_NUM = c.CG_NUM) AS [Nb codes taxe utilisant le compte],
                    CASE WHEN ISNULL(c.TA_CODE, '') = ''
                          AND NOT EXISTS (SELECT 1 FROM F_ETAXE e WHERE e.CG_NUM = c.CG_NUM)
                          AND NOT EXISTS (SELECT 1 FROM F_TAXE t WHERE t.CG_NUM = c.CG_NUM)
                         THEN 'Compte de TVA sans code taxe' ELSE '' END AS [Anomalie]
            FROM F_COMPTEG c
            WHERE c.N_Nature = 0 AND c.CG_Type = 0
              AND (c.CG_Num LIKE '4452%' OR c.CG_Num LIKE '4456%' OR c.CG_Num LIKE '4457%')
              AND c.CG_Num NOT LIKE '44567%' AND c.CG_Num NOT IN ({ComptesParametrage})
            ORDER BY c.CG_NUM
            """,
            "Anomalie"),

        new("4.9", "Codes taxe sans compte valide",
            "Codes taxe sans compte, avec un compte inexistant ou hors classe 445.",
            """
            SELECT  t.TA_Code AS [Code taxe],
                    t.TA_Intitule AS [Intitulé taxe],
                    FORMAT(t.TA_Taux, '0.######') + '%' AS [Taux],
                    ISNULL(t.CG_Num, '') AS [Compte taxe],
                    CASE WHEN ISNULL(t.CG_Num, '') = '' THEN 'Code taxe sans compte'
                         WHEN NOT EXISTS (SELECT 1 FROM F_COMPTEG g WHERE g.CG_Num = t.CG_Num) THEN 'Compte inexistant'
                         WHEN t.CG_Num NOT LIKE '445%' THEN 'Compte hors classe 445'
                         ELSE '' END AS [Anomalie]
            FROM F_TAXE t
            WHERE t.TA_TTAUX = 0 AND t.TA_TYPE IN (0, 1, 5)
            ORDER BY t.TA_Regroup, t.TA_Code
            """,
            "Anomalie"),

        new("4.10", "Cohérence du paramétrage fiscal",
            "Régime fiscal, journal OD et comptes de TVA renseignés et existants.",
            $"""
            ;WITH p AS (
                SELECT f.F_REGIME AS Regime, f.JO_NumMAJ AS Jo, f.CG_NumMAJDecaisser AS Decaisser,
                       f.CG_NumMAJCredit AS Credit, f.CG_NumMAJEcartD AS EcartD, f.CG_NumMAJEcartC AS EcartC,
                       d.D_TvaEncReg AS Enc
                FROM P_FISCAL f CROSS JOIN P_DOSSIER d
            )
            SELECT [Contrôle], [Valeur], [Anomalie] FROM (
                SELECT 1 AS Ord, N'Régime fiscal' AS [Contrôle],
                       CASE CAST(p.Regime AS varchar(5)) WHEN '0' THEN 'CA3 - Réel normal' WHEN '1' THEN 'CA3 - Mini réel'
                            WHEN '2' THEN 'CA12/CA12E - Réel simplifié' WHEN '3' THEN 'CA3 - Réel simplifié agriculture'
                            ELSE '' END AS [Valeur],
                       CASE WHEN p.Regime IS NULL OR CAST(p.Regime AS varchar(5)) NOT IN ('0','1','2','3')
                            THEN N'Régime fiscal non renseigné' ELSE '' END AS [Anomalie]
                FROM p
                UNION ALL
                SELECT 2, N'Journal OD de TVA', ISNULL(p.Jo, ''),
                       CASE WHEN ISNULL(p.Jo, '') = '' THEN N'Journal OD de TVA non renseigné'
                            WHEN NOT EXISTS (SELECT 1 FROM F_JOURNAUX j WHERE j.JO_Num = p.Jo) THEN N'Journal inexistant'
                            WHEN EXISTS (SELECT 1 FROM F_JOURNAUX j WHERE j.JO_Num = p.Jo AND j.JO_Type <> 3)
                                 THEN N'Le journal n''est pas de type Général'
                            ELSE '' END
                FROM p
                UNION ALL
                {ControleCompte(3, "Compte TVA à décaisser", "Decaisser", "Compte TVA à décaisser non renseigné")}
                UNION ALL
                {ControleCompte(4, "Compte crédit de TVA", "Credit", "Compte crédit de TVA non renseigné")}
                UNION ALL
                {ControleCompte(5, "Compte écart TVA débit", "EcartD", "Compte d'écart débit non renseigné")}
                UNION ALL
                {ControleCompte(6, "Compte écart TVA crédit", "EcartC", "Compte d'écart crédit non renseigné")}
                UNION ALL
                SELECT 7, N'Encaissement sur registre',
                       CASE WHEN p.Enc = 1 THEN 'Oui' ELSE 'Non' END,
                       CASE WHEN ISNULL(p.Enc, 0) = 0 AND EXISTS (SELECT 1 FROM F_TAXE t WHERE t.TA_TYPE = 1 AND t.TA_TTAUX = 0)
                            THEN N'Codes TVA/Encaissement présents alors que l''encaissement sur registre est désactivé (à vérifier)'
                            ELSE '' END
                FROM p
            ) x
            ORDER BY Ord
            """,
            "Anomalie"),

        new("4.11", "Écritures de TVA sans code taxe (N et N-1)",
            "Lignes d'écriture sur comptes 4452/4456/4457 sans code taxe, hors journal OD de TVA et comptes du paramétrage fiscal.",
            $"""
            SELECT  e.CG_NUM AS [N° compte],
                    g.CG_Intitule AS [Intitulé compte],
                    YEAR(e.JM_Date) AS [Année],
                    COUNT(*) AS [Nb lignes],
                    SUM(CASE WHEN ISNULL(e.TA_CODE, '') = '' THEN 1 ELSE 0 END) AS [Nb lignes sans code taxe],
                    CASE WHEN SUM(CASE WHEN ISNULL(e.TA_CODE, '') = '' THEN 1 ELSE 0 END) > 0
                         THEN 'Écritures sans code taxe' ELSE '' END AS [Anomalie]
            FROM F_ECRITUREC e
            JOIN F_COMPTEG g ON g.CG_NUM = e.CG_NUM
            WHERE g.N_Nature = 0 AND g.CG_Type = 0
              AND (e.CG_Num LIKE '4452%' OR e.CG_Num LIKE '4456%' OR e.CG_Num LIKE '4457%')
              AND e.CG_Num NOT LIKE '44567%' AND e.CG_Num NOT IN ({ComptesParametrage})
              AND e.JO_Num <> ISNULL((SELECT JO_NumMAJ FROM P_FISCAL), '')
              AND e.JM_Date >= DATEFROMPARTS(YEAR(GETDATE()) - 1, 1, 1)
            GROUP BY e.CG_NUM, g.CG_Intitule, YEAR(e.JM_Date)
            ORDER BY e.CG_NUM, YEAR(e.JM_Date)
            """,
            "Anomalie"),
    ];
}
