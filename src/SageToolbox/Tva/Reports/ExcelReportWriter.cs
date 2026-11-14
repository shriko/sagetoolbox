using ClosedXML.Excel;

namespace SageToolbox.Tva.Reports;

public sealed class ExcelReportWriter : IReportWriter
{
    private static readonly XLColor HeaderColor = XLColor.FromHtml("#1F3864");
    private static readonly XLColor AnomalyColor = XLColor.FromHtml("#F8CBAD");

    public string Extension => ".xlsx";

    public void Write(string path, ReportModel model)
    {
        using var wb = new XLWorkbook();

        var summary = wb.Worksheets.Add("Synthèse");
        summary.Cell(1, 1).Value = model.Title;
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 16;
        summary.Cell(2, 1).Value = $"Serveur : {model.Server} — généré le {model.GeneratedAt:dd/MM/yyyy HH:mm}";

        var cols = new[] { "Base", "Société", "Taille (Mo)", "Écritures N", "Écritures N-1", "Anomalies", "Contrôles en erreur" };
        for (var c = 0; c < cols.Length; c++) summary.Cell(4, c + 1).Value = cols[c];
        StyleHeader(summary.Range(4, 1, 4, cols.Length));

        var r = 5;
        foreach (var b in model.Bases)
        {
            summary.Cell(r, 1).Value = b.Info.Nom;
            summary.Cell(r, 2).Value = b.Info.Societe;
            summary.Cell(r, 3).Value = b.Info.TailleMo;
            summary.Cell(r, 4).Value = b.Info.NbEcrituresN;
            summary.Cell(r, 5).Value = b.Info.NbEcrituresN1;
            summary.Cell(r, 6).Value = b.TotalAnomalies;
            summary.Cell(r, 7).Value = b.TotalErrors;
            if (b.TotalAnomalies > 0 || b.TotalErrors > 0)
                summary.Range(r, 1, r, cols.Length).Style.Fill.BackgroundColor = AnomalyColor;
            r++;
        }
        summary.Columns().AdjustToContents();

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Synthèse" };
        foreach (var b in model.Bases)
        {
            var ws = wb.Worksheets.Add(UniqueSheetName(b.Info.Nom, usedNames));
            var row = 1;
            ws.Cell(row, 1).Value = $"{b.Info.Societe} ({b.Info.Nom}) — {b.TotalAnomalies} anomalie(s)";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            row += 2;

            foreach (var res in b.Results)
            {
                ws.Cell(row, 1).Value = $"{res.Definition.Id}  {res.Definition.Title}";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 12;
                row++;
                ws.Cell(row, 1).Value = res.Definition.Description;
                ws.Cell(row, 1).Style.Font.Italic = true;
                row++;

                if (res.Error is not null)
                {
                    ws.Cell(row, 1).Value = "Erreur d'exécution : " + res.Error;
                    ws.Cell(row, 1).Style.Font.FontColor = XLColor.Red;
                    row += 2;
                    continue;
                }
                if (res.Rows.Count == 0)
                {
                    ws.Cell(row, 1).Value = "Aucun résultat.";
                    row += 2;
                    continue;
                }

                for (var c = 0; c < res.Columns.Length; c++) ws.Cell(row, c + 1).Value = res.Columns[c];
                StyleHeader(ws.Range(row, 1, row, res.Columns.Length));
                row++;

                for (var i = 0; i < res.Rows.Count; i++)
                {
                    for (var c = 0; c < res.Columns.Length; c++)
                        ws.Cell(row, c + 1).Value = res.Rows[i][c]; // texte : conserve les zéros de tête des comptes
                    if (res.AnomalyRows.Contains(i))
                        ws.Range(row, 1, row, res.Columns.Length).Style.Fill.BackgroundColor = AnomalyColor;
                    row++;
                }
                row++;
            }
            ws.Columns().AdjustToContents(1, 80);
        }

        wb.SaveAs(path);
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = HeaderColor;
    }

    private static string UniqueSheetName(string raw, HashSet<string> used)
    {
        var clean = new string(raw.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray());
        if (clean.Length == 0) clean = "Base";
        if (clean.Length > 31) clean = clean[..31];

        var name = clean;
        for (var n = 2; !used.Add(name); n++)
        {
            var suffix = $"_{n}";
            name = clean[..Math.Min(clean.Length, 31 - suffix.Length)] + suffix;
        }
        return name;
    }
}
