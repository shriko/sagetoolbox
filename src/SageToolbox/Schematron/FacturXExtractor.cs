using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace SageToolbox.Schematron;

/// <summary>Extraction du XML embarqué dans un PDF Factur-X / ZUGFeRD.</summary>
public static class FacturXExtractor
{
    // Noms de pièce jointe usuels pour la facture XML (comparés en minuscules).
    private static readonly string[] XmlAttachmentNames = ["factur-x.xml", "zugferd-invoice.xml", "xrechnung.xml", "order-x.xml"];

    public static bool IsPdf(byte[] data)
    {
        var head = data.AsSpan(0, Math.Min(1024, data.Length)).TrimStart((byte)' ').TrimStart((byte)'\r').TrimStart((byte)'\n');
        return head.StartsWith("%PDF"u8);
    }

    /// <summary>Retourne le XML de facture joint au PDF ; lève <see cref="ConformityException"/> s'il est introuvable.</summary>
    public static byte[] ExtractXml(byte[] pdfBytes)
    {
        var attachments = new Dictionary<string, byte[]>();
        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            if (document.Advanced.TryGetEmbeddedFiles(out var files))
            {
                foreach (var file in files)
                {
                    var bytes = file.Bytes.ToArray();
                    if (bytes.Length > 0) attachments.TryAdd(file.Name.ToLowerInvariant(), bytes);
                }
            }
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new ConformityException("Le PDF est chiffré : impossible d'en extraire le XML.");
        }
        catch (Exception ex)
        {
            throw new ConformityException($"PDF illisible : {ex.Message}");
        }

        foreach (var wanted in XmlAttachmentNames)
            if (attachments.TryGetValue(wanted, out var data)) return data;

        var xmlNames = attachments.Keys.Where(n => n.EndsWith(".xml")).ToList();
        if (xmlNames.Count == 1) return attachments[xmlNames[0]];
        if (xmlNames.Count > 1)
            throw new ConformityException("Plusieurs XML joints au PDF, aucun ne porte un nom Factur-X connu : " + string.Join(", ", xmlNames));

        throw new ConformityException("Aucun XML joint dans ce PDF : ce n'est pas un Factur-X (pièce jointe « factur-x.xml » attendue).");
    }
}
