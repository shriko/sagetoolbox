using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SageToolbox.Tva.Reports;

public sealed class PdfReportWriter : IReportWriter
{
    private const string HeaderFill = "#1F3864";
    private const string AnomalyFill = "#F8CBAD";
    private const string StripeFill = "#F2F2F2";

    static PdfReportWriter()
    {
        // Licence Community de QuestPDF : gratuite sous 1 M$ de chiffre d'affaires annuel.
        QuestPDF.Settings.License = LicenseType.Community;
        // Polices système (Segoe UI) : la police Lato par défaut de QuestPDF est un fichier séparé
        // (QuestPDF.Fonts.Lato.br) qui n'est pas livré avec l'exécutable unique de la Toolbox ;
        // son absence ne doit pas bloquer la génération.
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    public string Extension => ".pdf";

    public void Write(string path, ReportModel model)
    {
        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Configure(page, model);
                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text("Synthèse").FontSize(16).Bold();
                    col.Item().Element(c => Table(c,
                        ["Base", "Société", "Taille (Mo)", "Écritures N", "Écritures N-1", "Anomalies", "Erreurs"],
                        model.Bases.Select(b => new[]
                        {
                            b.Info.Nom, b.Info.Societe, b.Info.TailleMo.ToString("N2"), b.Info.NbEcrituresN.ToString("N0"),
                            b.Info.NbEcrituresN1.ToString("N0"), b.TotalAnomalies.ToString(), b.TotalErrors.ToString()
                        }).ToList(),
                        model.Bases.Select((b, i) => (b, i)).Where(x => x.b.TotalAnomalies > 0 || x.b.TotalErrors > 0).Select(x => x.i).ToHashSet()));
                    col.Item().Text("Les lignes surlignées en orange sont des anomalies détectées par les contrôles automatiques.")
                        .Italic().FontSize(8);
                });
            });

            foreach (var b in model.Bases)
            {
                doc.Page(page =>
                {
                    Configure(page, model);
                    page.Content().Column(col =>
                    {
                        col.Spacing(6);
                        col.Item().Text($"{b.Info.Societe} ({b.Info.Nom})").FontSize(16).Bold();
                        col.Item().Text($"{b.TotalAnomalies} anomalie(s)" + (b.TotalErrors > 0 ? $" — {b.TotalErrors} contrôle(s) en erreur" : "")).Bold();

                        foreach (var r in b.Results)
                        {
                            col.Item().PaddingTop(8).Text($"{r.Definition.Id}  {r.Definition.Title}").FontSize(12).Bold();
                            col.Item().Text(r.Definition.Description).Italic().FontSize(8);

                            if (r.Error is not null)
                                col.Item().Text($"Erreur d'exécution : {r.Error}").Bold().FontColor(Colors.Red.Darken2);
                            else if (r.Rows.Count == 0)
                                col.Item().Text("Aucun résultat.");
                            else
                            {
                                if (r.Definition.AnomalyColumn is not null)
                                    col.Item().Text($"{r.AnomalyCount} anomalie(s) sur {r.Rows.Count} ligne(s).").Bold();
                                col.Item().Element(c => Table(c, r.Columns, r.Rows, r.AnomalyRows));
                            }
                        }
                    });
                });
            }
        }).GeneratePdf(path);
    }

    private static void Configure(PageDescriptor page, ReportModel model)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(25);
        page.DefaultTextStyle(t => t.FontFamily("Segoe UI").FontSize(8));
        page.Header().Column(h =>
        {
            h.Item().Text(model.Title).FontSize(11).Bold();
            h.Item().Text($"Serveur : {model.Server} — généré le {model.GeneratedAt:dd/MM/yyyy HH:mm}").FontSize(8);
        });
        page.Footer().AlignCenter().Text(t =>
        {
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    private static void Table(IContainer container, string[] columns, List<string[]> rows, HashSet<int> highlighted)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c => { foreach (var _ in columns) c.RelativeColumn(); });

            t.Header(h =>
            {
                foreach (var col in columns)
                    h.Cell().Background(HeaderFill).Padding(3).Text(col).FontColor(Colors.White).Bold();
            });

            for (var i = 0; i < rows.Count; i++)
            {
                var fill = highlighted.Contains(i) ? AnomalyFill : i % 2 == 1 ? StripeFill : Colors.White;
                foreach (var v in rows[i])
                    t.Cell().Background(fill).BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(v);
            }
        });
    }
}
