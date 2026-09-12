using System.Data;
using SageToolbox.Core;
using SageToolbox.Models;

namespace SageToolbox.Repositories;

/// <summary>
/// Contrôles d'intégrité comptable (#9), strictement lecture seule, plafonnés à 200
/// lignes par contrôle (anti-flood UI, cf. point d'architecture #5).
///
/// Colonnes confirmées dans le PDF Sage 100 "Structure des fichiers v1220" :
/// F_ECRITUREC (p.104-105) : CG_Num (compte général), CT_Num (compte tiers),
///   JO_Num (journal), EC_Piece, EC_Date, EC_No (clé technique), et le montant est
///   porté par UN SEUL champ signé EC_Montant + EC_Sens (0=Débit, 1=Crédit) — pas deux
///   colonnes débit/crédit séparées comme on pourrait le supposer.
/// F_COMPTEG (p.41) : PK CG_Num. F_COMPTET (p.56) : PK CT_Num. F_JOURNAUX (p.75) : PK JO_Num.
/// F_COMPTEA (p.49) : PK composite (N_Analytique, CA_Num).
/// F_ECRITUREA (p.109) : EC_No (lien F_ECRITUREC), (N_Analytique, CA_Num) (lien F_COMPTEA).
/// </summary>
public sealed class IntegrityRepository
{
    private const int RowCap = 200;

    public async Task<List<IntegrityIssue>> ComptesGenerauxManquantsAsync()
    {
        const string sql = @"
            SELECT DISTINCT TOP (@cap) ec.CG_Num
            FROM dbo.F_ECRITUREC ec
            LEFT JOIN dbo.F_COMPTEG cg ON ec.CG_Num = cg.CG_Num
            WHERE cg.CG_Num IS NULL AND ec.CG_Num IS NOT NULL AND ec.CG_Num <> ''";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Compte général référencé mais absent de F_COMPTEG", r => $"Compte {r["CG_Num"]}");
    }

    public async Task<List<IntegrityIssue>> ComptesTiersManquantsAsync()
    {
        const string sql = @"
            SELECT DISTINCT TOP (@cap) ec.CT_Num
            FROM dbo.F_ECRITUREC ec
            LEFT JOIN dbo.F_COMPTET ct ON ec.CT_Num = ct.CT_Num
            WHERE ct.CT_Num IS NULL AND ec.CT_Num IS NOT NULL AND ec.CT_Num <> ''";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Compte tiers référencé mais absent de F_COMPTET", r => $"Compte {r["CT_Num"]}");
    }

    public async Task<List<IntegrityIssue>> JournauxManquantsAsync()
    {
        const string sql = @"
            SELECT DISTINCT TOP (@cap) ec.JO_Num
            FROM dbo.F_ECRITUREC ec
            LEFT JOIN dbo.F_JOURNAUX jo ON ec.JO_Num = jo.JO_Num
            WHERE jo.JO_Num IS NULL AND ec.JO_Num IS NOT NULL AND ec.JO_Num <> ''";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Code journal référencé mais absent de F_JOURNAUX", r => $"Journal {r["JO_Num"]}");
    }

    /// <summary>
    /// Équilibre débit/crédit par journal et par mois (remplace un premier contrôle par
    /// pièce jugé peu pertinent — une pièce peut légitimement s'étaler sur plusieurs
    /// écritures complémentaires ; le vrai invariant comptable Sage est l'équilibre du
    /// journal sur la période).
    /// </summary>
    public async Task<List<IntegrityIssue>> JournalMoisDesequilibreAsync()
    {
        const string sql = @"
            SELECT TOP (@cap) JO_Num, YEAR(JM_Date) AS Annee, MONTH(JM_Date) AS Mois,
                   SUM(CASE WHEN EC_Sens = 0 THEN EC_Montant ELSE -EC_Montant END) AS Ecart
            FROM dbo.F_ECRITUREC
            GROUP BY JO_Num, YEAR(JM_Date), MONTH(JM_Date)
            HAVING ABS(SUM(CASE WHEN EC_Sens = 0 THEN EC_Montant ELSE -EC_Montant END)) > 0.01
            ORDER BY JO_Num, Annee, Mois";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Journal déséquilibré sur le mois",
            r => $"Journal {r["JO_Num"]} — {r["Mois"]:00}/{r["Annee"]} — écart {r["Ecart"]}");
    }

    /// <summary>
    /// Comptes généraux exigeant une ventilation analytique (F_COMPTEG.CG_Analytique <> 0,
    /// confirmé sur base réelle) mais dont l'écriture n'a aucune ligne F_ECRITUREA liée.
    /// </summary>
    public async Task<List<IntegrityIssue>> ComptesSansAnalytiqueAsync()
    {
        const string sql = @"
            SELECT TOP (@cap) ec.JO_Num, ec.JM_Date, ec.CG_Num, ec.EC_Piece
            FROM dbo.F_ECRITUREC ec
            JOIN dbo.F_COMPTEG cg ON ec.CG_Num = cg.CG_Num
            WHERE cg.CG_Analytique <> 0
              AND NOT EXISTS (SELECT 1 FROM dbo.F_ECRITUREA ea WHERE ea.EC_No = ec.EC_No)
            ORDER BY ec.JO_Num, ec.JM_Date";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Compte général attendant de l'analytique mais sans ventilation",
            r => $"Journal {r["JO_Num"]} / {r["JM_Date"]:d} / Compte {r["CG_Num"]} / Pièce {r["EC_Piece"]}");
    }

    public async Task<List<IntegrityIssue>> EcrituresAnalytiquesOrphelinesAsync()
    {
        const string sql = @"
            SELECT TOP (@cap) ea.EC_No
            FROM dbo.F_ECRITUREA ea
            LEFT JOIN dbo.F_ECRITUREC ec ON ea.EC_No = ec.EC_No
            WHERE ec.EC_No IS NULL";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Écriture analytique orpheline (F_ECRITUREA sans écriture comptable)",
            r => $"EC_No {r["EC_No"]}");
    }

    public async Task<List<IntegrityIssue>> EcrituresAnalytiquesCompteInexistantAsync()
    {
        const string sql = @"
            SELECT TOP (@cap) ea.EC_No, ea.N_Analytique, ea.CA_Num
            FROM dbo.F_ECRITUREA ea
            LEFT JOIN dbo.F_COMPTEA ca ON ea.N_Analytique = ca.N_Analytique AND ea.CA_Num = ca.CA_Num
            WHERE ca.CA_Num IS NULL AND ea.CA_Num IS NOT NULL AND ea.CA_Num <> ''";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Écriture analytique pointant vers un compte analytique inexistant",
            r => $"EC_No {r["EC_No"]} — section {r["N_Analytique"]} / compte {r["CA_Num"]}");
    }

    public async Task<List<IntegrityIssue>> RunAllAsync()
    {
        var issues = new List<IntegrityIssue>();
        issues.AddRange(await ComptesGenerauxManquantsAsync());
        issues.AddRange(await ComptesTiersManquantsAsync());
        issues.AddRange(await JournauxManquantsAsync());
        issues.AddRange(await JournalMoisDesequilibreAsync());
        issues.AddRange(await EcrituresAnalytiquesOrphelinesAsync());
        issues.AddRange(await EcrituresAnalytiquesCompteInexistantAsync());
        issues.AddRange(await ComptesSansAnalytiqueAsync());
        return issues;
    }

    private static Dictionary<string, object?> Cap() => new() { ["@cap"] = RowCap };

    private static List<IntegrityIssue> ToIssues(DataTable table, string controle, Func<DataRow, string> detail) =>
        table.Rows.Cast<DataRow>().Select(r => new IntegrityIssue { Controle = controle, Detail = detail(r) }).ToList();
}
