using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SageToolbox.Tva.Reports;

public sealed class WordReportWriter : IReportWriter
{
    private const string HeaderFill = "1F3864";
    private const string AnomalyFill = "F8CBAD";
    private const string StripeFill = "F2F2F2";

    public string Extension => ".docx";

    public void Write(string path, ReportModel model)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document();
        var body = main.Document.AppendChild(new Body());

        body.Append(Para(model.Title, size: 40, bold: true, outline: 0));
        body.Append(Para($"Serveur : {model.Server}  —  Généré le {model.GeneratedAt:dd/MM/yyyy HH:mm}", size: 20));

        body.Append(Para("Synthèse", size: 30, bold: true, outline: 0, before: 240));
        body.Append(SummaryTable(model));
        body.Append(Para("Les lignes surlignées en orange sont des anomalies détectées par les contrôles automatiques.", size: 18, italic: true, before: 120));

        foreach (var b in model.Bases)
        {
            body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            body.Append(Para($"{b.Info.Societe} ({b.Info.Nom})", size: 32, bold: true, outline: 0));
            body.Append(Para($"{b.TotalAnomalies} anomalie(s)" + (b.TotalErrors > 0 ? $" — {b.TotalErrors} contrôle(s) en erreur" : ""), size: 20, bold: true));

            foreach (var r in b.Results)
            {
                body.Append(Para($"{r.Definition.Id}  {r.Definition.Title}", size: 26, bold: true, outline: 1, before: 240));
                body.Append(Para(r.Definition.Description, size: 18, italic: true));

                if (r.Error is not null)
                {
                    body.Append(Para($"Erreur d'exécution : {r.Error}", size: 20, bold: true, color: "C00000"));
                    continue;
                }
                if (r.Rows.Count == 0)
                {
                    body.Append(Para("Aucun résultat.", size: 20));
                    continue;
                }
                if (r.Definition.AnomalyColumn is not null)
                    body.Append(Para($"{r.AnomalyCount} anomalie(s) sur {r.Rows.Count} ligne(s).", size: 20, bold: true));

                body.Append(DataTable(r.Columns, r.Rows, r.AnomalyRows));
            }
        }

        body.Append(new SectionProperties(
            new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape },
            new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720, Header = 360, Footer = 360, Gutter = 0 }));
    }

    private static Table SummaryTable(ReportModel model)
    {
        var columns = new[] { "Base", "Société", "Taille (Mo)", "Écritures N", "Écritures N-1", "Anomalies", "Contrôles en erreur" };
        var rows = model.Bases.Select(b => new[]
        {
            b.Info.Nom, b.Info.Societe, b.Info.TailleMo.ToString("N2"), b.Info.NbEcrituresN.ToString("N0"),
            b.Info.NbEcrituresN1.ToString("N0"), b.TotalAnomalies.ToString(), b.TotalErrors.ToString()
        }).ToList();
        var flagged = rows.Select((_, i) => i).Where(i => model.Bases[i].TotalAnomalies > 0 || model.Bases[i].TotalErrors > 0).ToHashSet();
        return DataTable(columns, rows, flagged);
    }

    private static Table DataTable(string[] columns, List<string[]> rows, HashSet<int> highlighted)
    {
        var table = new Table(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                Border<TopBorder>(), Border<BottomBorder>(), Border<LeftBorder>(),
                Border<RightBorder>(), Border<InsideHorizontalBorder>(), Border<InsideVerticalBorder>()),
            new TableCellMarginDefault(
                new TopMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 70, Type = TableWidthValues.Dxa },
                new TableCellRightMargin { Width = 70, Type = TableWidthValues.Dxa })));

        table.Append(new TableGrid(columns.Select(_ => new GridColumn())));

        var header = new TableRow(new TableRowProperties(new TableHeader()));
        foreach (var c in columns)
            header.Append(Cell(c, bold: true, color: "FFFFFF", fill: HeaderFill));
        table.Append(header);

        for (var i = 0; i < rows.Count; i++)
        {
            var fill = highlighted.Contains(i) ? AnomalyFill : i % 2 == 1 ? StripeFill : null;
            var row = new TableRow(new TableRowProperties(new CantSplit()));
            foreach (var v in rows[i])
                row.Append(Cell(v, fill: fill));
            table.Append(row);
        }
        return table;
    }

    private static T Border<T>() where T : BorderType, new() =>
        new() { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" };

    private static TableCell Cell(string text, bool bold = false, string? color = null, string? fill = null)
    {
        var props = new TableCellProperties();
        if (fill is not null)
            props.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fill, Color = "auto" });
        return new TableCell(props, Para(text, size: 16, bold: bold, color: color));
    }

    private static Paragraph Para(string text, int size = 22, bool bold = false, bool italic = false,
        string? color = null, int? outline = null, int before = 0)
    {
        var pp = new ParagraphProperties(new SpacingBetweenLines { Before = before.ToString(), After = "60" });
        if (outline is not null) pp.Append(new OutlineLevel { Val = outline.Value });

        var rp = new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
        if (bold) rp.Append(new Bold());
        if (italic) rp.Append(new Italic());
        if (color is not null) rp.Append(new Color { Val = color });
        rp.Append(new FontSize { Val = size.ToString() });

        return new Paragraph(pp, new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}
