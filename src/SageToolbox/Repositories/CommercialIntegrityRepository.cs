using System.Data;
using SageToolbox.Core;
using SageToolbox.Models;

namespace SageToolbox.Repositories;

/// <summary>
/// Contrôles d'intégrité gestion commerciale (#10), lecture seule, plafonnés à 200 lignes.
///
/// Colonnes confirmées dans le PDF Sage 100 "Structure des fichiers v1220" (p.242-266) :
/// F_DOCENTETE : DO_Domaine (0=Vente, 1=Achat, 2=Stock, 3=Ticket, 4=Interne), DO_Tiers
///   (FK F_COMPTET — pour un document Stock, contient le n° de dépôt, pas un tiers),
///   DE_No (dépôt de stockage), CO_No (collaborateur/représentant), CT_NumPayeur
///   ("Compte payeur ou encaisseur" — UN SEUL champ pour les deux rôles, le PDF ne
///   distingue pas payeur/encaisseur comme deux colonnes séparées), clé (DO_Type, DO_Piece).
/// F_DOCLIGNE : clé de rattachement à l'entête confirmée par l'index IDL_LIGNE =
///   (DO_Type, DO_Piece, DL_Ligne).
///
/// Non confirmées dans le PDF (absentes de la section extraite) : le nom de la table des
/// dépôts (supposé F_DEPOT, PK DE_No par cohérence de préfixe) et la clé primaire de
/// F_COLLABORATEUR (supposée CO_No, même préfixe que F_DOCENTETE.CO_No). Ces deux
/// contrôles se désactivent proprement (avec un log informatif) si la table/colonne
/// supposée n'existe pas, plutôt que d'échouer.
/// </summary>
public sealed class CommercialIntegrityRepository
{
    private const int RowCap = 200;

    public async Task<List<IntegrityIssue>> TiersInexistantAsync()
    {
        const string sql = @"
            SELECT DISTINCT TOP (@cap) DO_Type, DO_Piece, DO_Tiers
            FROM dbo.F_DOCENTETE de
            WHERE de.DO_Domaine IN (0, 1)
              AND de.DO_Tiers IS NOT NULL AND de.DO_Tiers <> ''
              AND NOT EXISTS (SELECT 1 FROM dbo.F_COMPTET ct WHERE ct.CT_Num = de.DO_Tiers)";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Tiers inexistant sur entête de document vente/achat",
            r => $"Pièce {r["DO_Piece"]} (type {r["DO_Type"]}) — tiers {r["DO_Tiers"]}");
    }

    public async Task<List<IntegrityIssue>> DepotInexistantAsync()
    {
        if (!await DatabaseManager.Instance.ObjectExistsAsync("F_DEPOT") ||
            !await DatabaseManager.Instance.ColumnExistsAsync("F_DEPOT", "DE_No"))
        {
            Logger.Instance.Info("Contrôle dépôt inexistant ignoré : table F_DEPOT (ou colonne DE_No) introuvable sur cette base.");
            return new List<IntegrityIssue>();
        }

        const string sql = @"
            SELECT DISTINCT TOP (@cap) DO_Type, DO_Piece, DE_No
            FROM dbo.F_DOCENTETE de
            WHERE de.DE_No IS NOT NULL AND de.DE_No <> ''
              AND NOT EXISTS (SELECT 1 FROM dbo.F_DEPOT dep WHERE dep.DE_No = de.DE_No)";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Dépôt de stockage inexistant",
            r => $"Pièce {r["DO_Piece"]} (type {r["DO_Type"]}) — dépôt {r["DE_No"]}");
    }

    public async Task<List<IntegrityIssue>> CollaborateurInexistantAsync()
    {
        if (!await DatabaseManager.Instance.ObjectExistsAsync("F_COLLABORATEUR") ||
            !await DatabaseManager.Instance.ColumnExistsAsync("F_COLLABORATEUR", "CO_No"))
        {
            Logger.Instance.Info("Contrôle collaborateur inexistant ignoré : table F_COLLABORATEUR (ou colonne CO_No) introuvable sur cette base.");
            return new List<IntegrityIssue>();
        }

        const string sql = @"
            SELECT DISTINCT TOP (@cap) DO_Type, DO_Piece, CO_No
            FROM dbo.F_DOCENTETE de
            WHERE de.CO_No IS NOT NULL AND de.CO_No <> ''
              AND NOT EXISTS (SELECT 1 FROM dbo.F_COLLABORATEUR co WHERE co.CO_No = de.CO_No)";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Collaborateur inexistant",
            r => $"Pièce {r["DO_Piece"]} (type {r["DO_Type"]}) — collaborateur {r["CO_No"]}");
    }

    public async Task<List<IntegrityIssue>> TiersPayeurInexistantAsync()
    {
        const string sql = @"
            SELECT DISTINCT TOP (@cap) DO_Type, DO_Piece, CT_NumPayeur
            FROM dbo.F_DOCENTETE de
            WHERE de.CT_NumPayeur IS NOT NULL AND de.CT_NumPayeur <> ''
              AND NOT EXISTS (SELECT 1 FROM dbo.F_COMPTET ct WHERE ct.CT_Num = de.CT_NumPayeur)";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Tiers payeur/encaisseur inexistant",
            r => $"Pièce {r["DO_Piece"]} (type {r["DO_Type"]}) — tiers payeur {r["CT_NumPayeur"]}");
    }

    public async Task<List<IntegrityIssue>> LignesOrphelinesAsync()
    {
        const string sql = @"
            SELECT TOP (@cap) dl.DO_Type, dl.DO_Piece, dl.DL_Ligne
            FROM dbo.F_DOCLIGNE dl
            LEFT JOIN dbo.F_DOCENTETE de ON dl.DO_Type = de.DO_Type AND dl.DO_Piece = de.DO_Piece
            WHERE de.DO_Piece IS NULL";
        var rows = await DatabaseManager.Instance.ExecuteQueryAsync(sql, Cap());
        return ToIssues(rows, "Ligne de document orpheline (sans entête correspondante)",
            r => $"Pièce {r["DO_Piece"]} (type {r["DO_Type"]}) — ligne {r["DL_Ligne"]}");
    }

    public async Task<List<IntegrityIssue>> RunAllAsync()
    {
        var issues = new List<IntegrityIssue>();
        issues.AddRange(await TiersInexistantAsync());
        issues.AddRange(await DepotInexistantAsync());
        issues.AddRange(await CollaborateurInexistantAsync());
        issues.AddRange(await TiersPayeurInexistantAsync());
        issues.AddRange(await LignesOrphelinesAsync());
        return issues;
    }

    private static Dictionary<string, object?> Cap() => new() { ["@cap"] = RowCap };

    private static List<IntegrityIssue> ToIssues(DataTable table, string controle, Func<DataRow, string> detail) =>
        table.Rows.Cast<DataRow>().Select(r => new IntegrityIssue { Controle = controle, Detail = detail(r) }).ToList();
}
