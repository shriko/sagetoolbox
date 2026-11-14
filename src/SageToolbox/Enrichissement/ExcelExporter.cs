using ClosedXML.Excel;

namespace SageToolbox.Enrichissement;

/// <summary>Exporte les lignes confirmées du tableau de résultats vers un classeur Excel (.xlsx).</summary>
public static class ExcelExporter
{
    private static readonly string[] Headers =
    {
        "CT_Num", "Nom Sage", "Adresse Sage", "Complément Sage", "CP Sage", "Ville Sage",
        "SIRET Sage", "Status", "SIRET API", "Nom API", "Adresse API",
    };

    public static void Export(IEnumerable<ResultRow> rows, string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Rapprochement SIRET");

        for (int col = 0; col < Headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = Headers[col];
            sheet.Cell(1, col + 1).Style.Font.Bold = true;
        }

        int rowIndex = 2;
        foreach (var row in rows.Where(r => r.Confirmed))
        {
            sheet.Cell(rowIndex, 1).Value = row.Tier.CT_Num;
            sheet.Cell(rowIndex, 2).Value = row.Tier.CT_Intitule;
            sheet.Cell(rowIndex, 3).Value = row.Tier.CT_Adresse;
            sheet.Cell(rowIndex, 4).Value = row.Tier.CT_Complement;
            sheet.Cell(rowIndex, 5).Value = row.Tier.CT_CodePostal;
            sheet.Cell(rowIndex, 6).Value = row.Tier.CT_Ville;
            sheet.Cell(rowIndex, 7).Value = row.Tier.CT_Siret;
            sheet.Cell(rowIndex, 8).Value = row.Status.ToDisplayString();
            sheet.Cell(rowIndex, 9).Value = row.SiretApi;
            sheet.Cell(rowIndex, 10).Value = row.NomApi;
            sheet.Cell(rowIndex, 11).Value = row.AdresseApi;
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
