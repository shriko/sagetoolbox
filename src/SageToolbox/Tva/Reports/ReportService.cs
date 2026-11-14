using System.IO;

namespace SageToolbox.Tva.Reports;

public interface IReportWriter
{
    string Extension { get; }
    void Write(string path, ReportModel model);
}

public static class ReportService
{
    private static readonly Dictionary<ReportFormats, IReportWriter> Writers = new()
    {
        [ReportFormats.Word] = new WordReportWriter(),
        [ReportFormats.Pdf] = new PdfReportWriter(),
        [ReportFormats.Excel] = new ExcelReportWriter()
    };

    /// <summary>Génère un rapport unique ou un rapport par base, dans chaque format demandé. Retourne les fichiers créés.</summary>
    public static List<string> Generate(IReadOnlyList<BaseAnalysis> analyses, string server, string outputDir,
        ReportFormats formats, bool onePerBase)
    {
        Directory.CreateDirectory(outputDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var files = new List<string>();

        var groups = onePerBase
            ? analyses.Select(a => (Name: $"Audit_TVA_{Sanitize(a.Info.Nom)}_{stamp}", Items: (IReadOnlyList<BaseAnalysis>)[a])).ToList()
            : [(Name: $"Audit_TVA_{stamp}", Items: analyses)];

        foreach (var (name, items) in groups)
        {
            var model = new ReportModel
            {
                Title = onePerBase ? $"Rapport Audit TVA — {items[0].Info.Societe}" : "Rapport Audit TVA",
                Server = server,
                Bases = items
            };

            foreach (var (format, writer) in Writers)
            {
                if (!formats.HasFlag(format)) continue;
                var path = Path.Combine(outputDir, name + writer.Extension);
                writer.Write(path, model);
                files.Add(path);
            }
        }
        return files;
    }

    private static string Sanitize(string s) =>
        string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
