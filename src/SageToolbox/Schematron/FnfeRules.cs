using System.IO;
using System.IO.Compression;
using System.Xml;

namespace SageToolbox.Schematron;

/// <summary>
/// Règles France_RFE (FNFE) 1.4.0.04 embarquées dans l'exécutable sous forme d'une archive zip
/// (sources .sch, XSLT précompilés, schémas XSD). Les chemins sont relatifs au dossier
/// FNFE_RFE_INVOICE du dépôt source (ex. « UBL/EN16931/2xslt/EN16931-UBL-validation.xslt »).
/// </summary>
internal static class FnfeRules
{
    private const string ResourceName = "fnfe_rules.zip";

    private static readonly object Gate = new();
    private static ZipArchive? _archive;

    private static ZipArchive Archive
    {
        get
        {
            if (_archive is not null) return _archive;
            using var resource = typeof(FnfeRules).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Ressource embarquée « {ResourceName} » introuvable.");
            var memory = new MemoryStream();
            resource.CopyTo(memory);
            memory.Position = 0;
            return _archive = new ZipArchive(memory, ZipArchiveMode.Read);
        }
    }

    public static byte[] ReadAllBytes(string path)
    {
        lock (Gate)
        {
            var entry = Archive.GetEntry(path) ?? throw new FileNotFoundException($"Règle embarquée introuvable : {path}");
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    /// <summary>
    /// Extrait sur disque (dossier temporaire) les fichiers annexes situés à côté d'un XSLT — listes de codes
    /// <c>*_codedb.xml</c> chargées par <c>document()</c> — et retourne l'URI fichier du XSLT, à utiliser comme
    /// URI de base pour que Saxon résolve ces références. Le XSLT lui-même reste lu depuis l'archive.
    /// </summary>
    public static Uri ExtractSidecarFiles(string xsltPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "SageToolbox", "fnfe_rules_1.4.0.04");
        var folder = xsltPath[..(xsltPath.LastIndexOf('/') + 1)];
        lock (Gate)
        {
            foreach (var entry in Archive.Entries.Where(e => e.FullName.StartsWith(folder, StringComparison.Ordinal)
                         && e.FullName.IndexOf('/', folder.Length) < 0 && !e.FullName.EndsWith(".xslt", StringComparison.Ordinal)))
            {
                var target = Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(target) && new FileInfo(target).Length == entry.Length) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }
        return new Uri(Path.Combine(root, xsltPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>URI factice « fnfe:///chemin » permettant de résoudre les imports relatifs entre fichiers de l'archive.</summary>
    public static Uri ToUri(string path) => new($"fnfe:///{path}");

    public static string FromUri(Uri uri) => Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');

    /// <summary>Résolveur XML lisant les schémas (xs:import / xs:include) dans l'archive embarquée.</summary>
    public sealed class ArchiveResolver : global::System.Xml.XmlResolver
    {
        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn) =>
            new MemoryStream(ReadAllBytes(FromUri(absoluteUri)));
    }
}
