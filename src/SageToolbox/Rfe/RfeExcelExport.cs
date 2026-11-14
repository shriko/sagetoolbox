using ClosedXML.Excel;

namespace SageToolbox.Rfe;

/// <summary>Colonnes à inclure dans l'export (la colonne « Compte » est toujours présente).</summary>
public sealed record RfeExcelColumns(
    bool RaisonSociale = true,
    bool Telephone = true,
    bool Email = true,
    bool TypeMaille = true,
    bool MailleAdresse = true,
    bool Actif = true,
    bool NbAdresses = true,
    bool Contact = true);

public static class RfeExcelExport
{
    private static readonly XLColor ActifFill = XLColor.FromArgb(0xC8, 0xE6, 0xC9);
    private static readonly XLColor InactifFill = XLColor.FromArgb(0xFF, 0xCD, 0xD2);

    public static void Export(string filePath, IEnumerable<RfeAddress> rows, RfeExcelColumns options)
    {
        var columns = BuildColumns(options);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Maillage");

        for (var col = 0; col < columns.Count; col++)
            sheet.Cell(1, col + 1).Value = columns[col].Header;
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var item in rows.OrderBy(r => r.Compte))
        {
            for (var col = 0; col < columns.Count; col++)
                sheet.Cell(row, col + 1).Value = columns[col].Value(item);

            sheet.Range(row, 1, row, columns.Count).Style.Fill.BackgroundColor = item.Actif == "Oui" ? ActifFill : InactifFill;
            row++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        workbook.SaveAs(filePath);
    }

    private static List<(string Header, Func<RfeAddress, string> Value)> BuildColumns(RfeExcelColumns options)
    {
        var columns = new List<(string, Func<RfeAddress, string>)> { ("Compte", r => r.Compte) };

        if (options.RaisonSociale) columns.Add(("Raison Sociale", r => r.RaisonSociale));
        if (options.Telephone) columns.Add(("Téléphone", r => r.Telephone));
        if (options.Email) columns.Add(("E-mail", r => r.Email));
        if (options.TypeMaille) columns.Add(("Type Maille", r => r.TypeMaille));
        if (options.MailleAdresse) columns.Add(("Maille Adresse", r => r.MailleAdresse));
        if (options.Actif) columns.Add(("Actif", r => r.Actif));
        if (options.NbAdresses) columns.Add(("Nb Adresses", r => r.NbAdresses.ToString()));
        if (options.Contact) columns.Add(("Contact", r => r.SelectedContact?.DisplayName ?? ""));

        return columns;
    }
}
