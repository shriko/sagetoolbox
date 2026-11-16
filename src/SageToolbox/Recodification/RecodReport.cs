using System.IO;
using System.Net;
using System.Text;

namespace SageToolbox.Recodification;

public sealed record ReportData(
    string Server,
    bool IsSpc,
    string Dossier,
    string? Schema,
    ElementDefinition Definition,
    IReadOnlyList<RecodItem> Changes,
    RecodResult Result,
    string BackupDescription);

public static class RecodReport
{
    public static string Write(ReportData d)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SageToolbox", "Recodification");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"rapport_{Sanitize(d.Dossier)}_{d.Definition.Kind}_{d.Result.Start:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(path, Render(d), Encoding.UTF8);
        return path;
    }

    public static string Render(ReportData d)
    {
        var r = d.Result;
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><title>Rapport de recodification</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;margin:2rem;color:#222}table{border-collapse:collapse;margin:.5rem 0 1.5rem}td,th{border:1px solid #ccc;padding:4px 10px;text-align:left}th{background:#f0f0f0}.ok{color:#1a7f37}.ko{color:#b3261e}.num{text-align:right}</style></head><body>");
        sb.AppendLine($"<h1>Rapport de recodification — {E(d.Definition.Title)}</h1>");
        sb.AppendLine($"<p class=\"{(r.Success ? "ok" : "ko")}\"><b>{(r.Success ? "Traitement terminé avec succès" : "ÉCHEC — toutes les modifications ont été annulées")}</b></p>");
        if (!r.Success) sb.AppendLine($"<p class=\"ko\">{E(r.Error)}</p>");

        sb.AppendLine("<table>");
        Row(sb, "Serveur", d.Server);
        Row(sb, "Mode", d.IsSpc ? "Sage Premium Cloud (SPC)" : "Sage 100 SQL Server");
        Row(sb, d.IsSpc ? "Société" : "Dossier (base)", d.Dossier);
        if (d.Schema is not null) Row(sb, "Schéma SQL", d.Schema);
        Row(sb, "Utilisateur Windows", Environment.UserName);
        Row(sb, "Début", r.Start.ToString("dd/MM/yyyy HH:mm:ss"));
        Row(sb, "Fin", r.End.ToString("dd/MM/yyyy HH:mm:ss"));
        Row(sb, "Durée", (r.End - r.Start).ToString(@"hh\:mm\:ss"));
        Row(sb, "Sauvegarde préalable", d.BackupDescription);
        Row(sb, "Codes recodifiés", r.MappingCount.ToString());
        if (r.Merged > 0) Row(sb, "dont fusions dans une fiche existante", r.Merged.ToString());
        Row(sb, "Instructions exécutées", r.ExecutedStatements.ToString());
        sb.AppendLine("</table>");

        if (r.Success)
        {
            sb.AppendLine("<h2>Lignes modifiées par table / colonne</h2><table><tr><th>Table.colonne</th><th>Lignes</th></tr>");
            foreach (var u in r.Updated) sb.AppendLine($"<tr><td>{E(u.Label)}</td><td class=\"num\">{u.Rows}</td></tr>");
            sb.AppendLine($"<tr><th>Total</th><th class=\"num\">{r.Updated.Sum(u => u.Rows)}</th></tr></table>");
        }

        if (r.Skipped.Count > 0)
        {
            sb.AppendLine("<h2>Instructions ignorées (structure absente de ce dossier)</h2><table><tr><th>Table.colonne</th><th>Raison</th></tr>");
            foreach (var s in r.Skipped) sb.AppendLine($"<tr><td>{E(s.Statement.Label)}</td><td>{E(s.Reason)}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("<h2>Correspondances</h2><table><tr>");
        if (d.Definition.HasPlan) sb.Append("<th>Plan</th>");
        sb.AppendLine("<th>Ancien code</th><th>Intitulé</th><th>Nouveau code</th><th>Opération</th></tr>");
        foreach (var c in d.Changes)
            sb.AppendLine($"<tr>{(d.Definition.HasPlan ? $"<td>{c.Plan}</td>" : "")}<td>{E(c.Code)}</td><td>{E(c.Label)}</td><td>{E(c.EffectiveNewCode)}</td><td>{(c.IsMerge ? "Fusion" : "Recodification")}</td></tr>");
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static void Row(StringBuilder sb, string k, string v) =>
        sb.AppendLine($"<tr><th>{WebUtility.HtmlEncode(k)}</th><td>{WebUtility.HtmlEncode(v)}</td></tr>");
}
