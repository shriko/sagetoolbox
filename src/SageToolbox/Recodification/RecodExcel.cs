using System.IO;
using ClosedXML.Excel;

namespace SageToolbox.Recodification;

public sealed record ImportResult(int Matched, IReadOnlyList<string> Unknown);

public static class RecodExcel
{
    private const string HPlan = "Plan";
    private const string HCode = "Code actuel";
    private const string HLabel = "Intitulé";
    private const string HNew = "Nouveau code";

    public static void Export(string path, ElementDefinition def, IEnumerable<RecodItem> items)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(def.Title.Length > 31 ? def.Title[..31] : def.Title);

        var headers = def.HasPlan ? new[] { HPlan, HCode, HLabel, HNew } : new[] { HCode, HLabel, HNew };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = 2;
        foreach (var i in items)
        {
            var col = 1;
            if (def.HasPlan) ws.Cell(row, col++).Value = i.Plan;
            ws.Cell(row, col++).Value = i.Code;
            ws.Cell(row, col++).Value = i.Label;
            ws.Cell(row, col).Value = i.NewCode;
            row++;
        }

        // Codes en texte : Excel ne doit ni supprimer les zéros de tête ni convertir en nombre.
        var codeCol = def.HasPlan ? 2 : 1;
        ws.Column(codeCol).Style.NumberFormat.Format = "@";
        ws.Column(headers.Length).Style.NumberFormat.Format = "@";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
    }

    /// <summary>Renseigne NewCode des éléments dont « Code actuel » (et plan) correspond à une ligne du fichier.</summary>
    public static ImportResult Import(string path, ElementDefinition def, IReadOnlyCollection<RecodItem> items)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var header = ws.Row(1);
        int Find(string name) => header.CellsUsed().FirstOrDefault(c => string.Equals(c.GetString().Trim(), name, StringComparison.OrdinalIgnoreCase))?.Address.ColumnNumber ?? 0;

        int codeCol = Find(HCode), newCol = Find(HNew), planCol = def.HasPlan ? Find(HPlan) : 0;
        if (codeCol == 0 || newCol == 0 || (def.HasPlan && planCol == 0))
            throw new InvalidDataException($"Colonnes attendues en ligne 1 : {(def.HasPlan ? HPlan + ", " : "")}{HCode}, {HNew}.");

        var index = items.ToDictionary(i => $"{i.Plan}|{i.Code}", StringComparer.OrdinalIgnoreCase);
        int matched = 0;
        var unknown = new List<string>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var code = row.Cell(codeCol).GetFormattedString().Trim();
            var nouveau = row.Cell(newCol).GetFormattedString().Trim();
            if (code.Length == 0) continue;
            var plan = def.HasPlan && int.TryParse(row.Cell(planCol).GetFormattedString().Trim(), out var p) ? p : 0;

            if (index.TryGetValue($"{plan}|{code}", out var item)) { item.NewCode = nouveau; matched++; }
            else unknown.Add(def.HasPlan ? $"{plan}/{code}" : code);
        }
        return new ImportResult(matched, unknown);
    }
}
