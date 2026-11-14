using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Saxon.Api;

namespace SageToolbox.Schematron;

// ---------------------------------------------------------------------------
// Modèles de données
// ---------------------------------------------------------------------------

public sealed record RuleFailure(string Location, string LocationSimple, string Text);

public sealed record RuleResult(
    string Id, string Flag, string Message, string Test, string Context, string PatternTitle, string RulesetLabel,
    string Status, int OccurrencesTotal, IReadOnlyList<RuleFailure> Failures)
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string NotApplicable = "not_applicable";
}

public sealed record XsdError(int Line, string Message);

public sealed record XsdResult(string Schema, bool Valid, IReadOnlyList<XsdError> Errors);

/// <summary>Point d'attention hors Schematron (« critical » ou « warning »).</summary>
public sealed record VigilancePoint(string Code, string Level, string Title, string Message);

public sealed class ConformityReport
{
    public required string RootElement { get; init; }
    public required string FormatLabel { get; init; }
    public required string CustomizationId { get; init; }
    public required string DetectedProfile { get; init; }
    public required Profile Profile { get; init; }
    public required IReadOnlyList<VigilancePoint> VigilancePoints { get; init; }
    public XsdResult? Xsd { get; init; }
    public required IReadOnlyList<RuleResult> Rules { get; init; }

    public int XsdErrorCount => Xsd?.Errors.Count ?? 0;
    public int FatalCount => Rules.Count(r => r.Status == RuleResult.Failed && r.Flag == "fatal");
    public int WarningCount => Rules.Count(r => r.Status == RuleResult.Failed && r.Flag == "warning");
    public int PassedCount => Rules.Count(r => r.Status == RuleResult.Passed);
    public int FailedCount => Rules.Count(r => r.Status == RuleResult.Failed);
    public int NotApplicableCount => Rules.Count(r => r.Status == RuleResult.NotApplicable);
    public int EvaluatedCount => PassedCount + FailedCount;
    public bool Conforme => FatalCount == 0 && XsdErrorCount == 0;
}

public sealed class ConformityException(string message) : Exception(message);

// ---------------------------------------------------------------------------
// Moteur : catalogue des règles (.sch) + exécution des XSLT précompilés (Saxon-HE) + validation XSD
// ---------------------------------------------------------------------------

/// <summary>
/// Contrôle de conformité d'un document électronique (UBL, CII, Factur-X ou CDAR) contre les
/// schematrons France_RFE. Chaque règle du .sch est croisée avec le rapport SVRL produit par le
/// XSLT précompilé pour déterminer, règle par règle : conforme, non conforme ou non applicable.
/// </summary>
public static class SchematronEngine
{
    private static readonly XNamespace Sch = "http://purl.oclc.org/dsdl/schematron";
    private static readonly XNamespace Svrl = "http://purl.oclc.org/dsdl/svrl";
    private static readonly XNamespace Cbc = Rulesets.CbcNamespace;
    private static readonly XNamespace Ram = Rulesets.RamNamespace;

    private sealed record RuleDef(string Id, string Test, string Flag, string Message, string Context, string PatternTitle);

    // ----- Catalogue des règles (.sch), mis en cache -----

    private static readonly Dictionary<string, Dictionary<(string Pattern, string Context), List<RuleDef>>> CatalogCache = new();
    private static readonly object CatalogGate = new();

    private static Dictionary<(string Pattern, string Context), List<RuleDef>> GetCatalog(SchematronSet set)
    {
        lock (CatalogGate)
        {
            if (CatalogCache.TryGetValue(set.SchPath, out var cached)) return cached;

            using var stream = new MemoryStream(FnfeRules.ReadAllBytes(set.SchPath));
            var tree = XDocument.Load(stream);
            var catalog = new Dictionary<(string, string), List<RuleDef>>();
            foreach (var pattern in tree.Descendants(Sch + "pattern"))
            {
                var patternId = (string?)pattern.Attribute("id") ?? (string?)pattern.Attribute("name") ?? "";
                var title = pattern.Element(Sch + "title")?.Value.Trim() ?? patternId;
                foreach (var rule in pattern.Elements(Sch + "rule"))
                {
                    var context = (string?)rule.Attribute("context") ?? "";
                    var defs = rule.Elements(Sch + "assert").Select(a => new RuleDef(
                        (string?)a.Attribute("id") ?? "", (string?)a.Attribute("test") ?? "",
                        (string?)a.Attribute("flag") ?? "error", a.Value.Trim(), context, title)).ToList();
                    if (defs.Count == 0) continue;
                    if (!catalog.TryGetValue((patternId, context), out var list))
                        catalog[(patternId, context)] = list = new List<RuleDef>();
                    list.AddRange(defs);
                }
            }
            return CatalogCache[set.SchPath] = catalog;
        }
    }

    // ----- Moteur XSLT (Saxon-HE), feuilles de style compilées mises en cache -----

    private static readonly object SaxonGate = new();
    private static Processor? _saxon;
    private static readonly Dictionary<string, XsltExecutable> XsltCache = new();

    private static string RunXslt(string xsltPath, byte[] xmlBytes)
    {
        lock (SaxonGate)
        {
            _saxon ??= new Processor(false);
            if (!XsltCache.TryGetValue(xsltPath, out var executable))
            {
                var compiler = _saxon.NewXsltCompiler();
                compiler.BaseUri = FnfeRules.ExtractSidecarFiles(xsltPath);
                using var xslt = new MemoryStream(FnfeRules.ReadAllBytes(xsltPath));
                XsltCache[xsltPath] = executable = compiler.Compile(xslt);
            }

            var builder = _saxon.NewDocumentBuilder();
            builder.BaseUri = new Uri("file:///document.xml");
            XdmNode document;
            using (var xml = new MemoryStream(xmlBytes)) document = builder.Build(xml);

            var output = new StringWriter();
            var transformer = executable.Load30();
            // Les variables globales des XSLT (ex. $invoiceID) sont évaluées sur le document : l'item de contexte global est requis.
            transformer.GlobalContextItem = document;
            transformer.ApplyTemplates(document, _saxon.NewSerializer(output));
            return output.ToString();
        }
    }

    // ----- Validation XSD (structure), schémas compilés mis en cache -----

    private static readonly Dictionary<string, XmlSchemaSet> XsdCache = new();
    private static readonly object XsdGate = new();

    private static XmlSchemaSet GetSchemaSet(string xsdPath)
    {
        lock (XsdGate)
        {
            if (XsdCache.TryGetValue(xsdPath, out var cached)) return cached;

            var resolver = new FnfeRules.ArchiveResolver();
            var set = new XmlSchemaSet { XmlResolver = resolver };
            var settings = new XmlReaderSettings { XmlResolver = resolver, DtdProcessing = DtdProcessing.Ignore };
            using var stream = new MemoryStream(FnfeRules.ReadAllBytes(xsdPath));
            using var reader = XmlReader.Create(stream, settings, FnfeRules.ToUri(xsdPath).AbsoluteUri);
            set.Add(null, reader);
            set.Compile();
            return XsdCache[xsdPath] = set;
        }
    }

    private static XsdResult ValidateXsd(string xsdPath, byte[] xmlBytes)
    {
        var set = GetSchemaSet(xsdPath);
        var errors = new List<XsdError>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = set, DtdProcessing = DtdProcessing.Ignore };
        settings.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
                errors.Add(new XsdError(e.Exception?.LineNumber ?? 0, e.Message));
        };
        using var stream = new MemoryStream(xmlBytes);
        using var reader = XmlReader.Create(stream, settings);
        while (reader.Read()) { }
        return new XsdResult(Path.GetFileName(xsdPath), errors.Count == 0, errors);
    }

    // ----- Analyse du rapport SVRL -----

    private static readonly Regex NamespacePredicate = new(@"\[namespace-uri\(\)='[^']*'\]", RegexOptions.Compiled);
    private static readonly Regex AnyNamespacePrefix = new(@"\*:", RegexOptions.Compiled);

    /// <summary>Rend l'XPath généré par Schematron plus lisible en repliant les prédicats de namespace.</summary>
    internal static string SimplifyXPath(string location) =>
        AnyNamespacePrefix.Replace(NamespacePredicate.Replace(location, ""), "");

    private sealed record SvrlFailure(string PatternId, string Context, string Id, string Flag, string Location, string Text);

    private static (Dictionary<(string, string), int> Fired, List<SvrlFailure> Failures) ParseSvrl(string svrl)
    {
        var root = XDocument.Parse(svrl).Root!;
        var fired = new Dictionary<(string, string), int>();
        var failures = new List<SvrlFailure>();
        var pattern = "";
        var context = "";
        foreach (var el in root.Elements())
        {
            switch (el.Name.LocalName)
            {
                case "active-pattern":
                    pattern = (string?)el.Attribute("id") ?? (string?)el.Attribute("name") ?? "";
                    context = "";
                    break;
                case "fired-rule":
                    context = (string?)el.Attribute("context") ?? "";
                    fired[(pattern, context)] = fired.GetValueOrDefault((pattern, context)) + 1;
                    break;
                case "failed-assert":
                    failures.Add(new SvrlFailure(pattern, context, (string?)el.Attribute("id") ?? "",
                        (string?)el.Attribute("flag") ?? "error", (string?)el.Attribute("location") ?? "",
                        el.Element(Svrl + "text")?.Value.Trim() ?? ""));
                    break;
            }
        }
        return (fired, failures);
    }

    private static List<RuleResult> RunSchematronSet(SchematronSet set, byte[] xmlBytes)
    {
        var catalog = GetCatalog(set);
        var (fired, failures) = ParseSvrl(RunXslt(set.XsltPath, xmlBytes));
        var failuresByRule = failures.ToLookup(f => (f.PatternId, f.Context, f.Id));

        var results = new List<RuleResult>();
        foreach (var ((patternId, context), defs) in catalog)
        {
            var occurrences = fired.GetValueOrDefault((patternId, context));
            foreach (var def in defs)
            {
                var failed = failuresByRule[(patternId, context, def.Id)].ToList();
                var status = occurrences == 0 ? RuleResult.NotApplicable : failed.Count > 0 ? RuleResult.Failed : RuleResult.Passed;
                results.Add(new RuleResult(def.Id, def.Flag, def.Message, def.Test, context, def.PatternTitle, set.Label,
                    status, occurrences,
                    failed.Select(f => new RuleFailure(f.Location, SimplifyXPath(f.Location), f.Text)).ToList()));
            }
        }
        return results;
    }

    // ----- Points de vigilance -----

    /// <summary>Détecte un caractère '#' superflu dans le texte libre d'une note BT-22 UBL.
    /// Convention France_RFE : une note commence par un préfixe « #CODE# » (code sujet UNTDID 4451)
    /// suivi du texte libre ; un « # » supplémentaire peut être pris à tort pour un nouveau segment
    /// de code par les plateformes de réception (Chorus Pro / PDP) et a déjà provoqué des rejets.</summary>
    private static IEnumerable<VigilancePoint> StrayHashNotePoints(string? formatKey, XElement root)
    {
        if (formatKey != "ubl") yield break;
        foreach (var note in root.Elements(Cbc + "Note"))
        {
            var text = note.Value;
            if (!text.StartsWith('#')) continue;
            var end = text.IndexOf('#', 1);
            if (end <= 0) continue;
            var remainder = text[(end + 1)..];
            if (!remainder.Contains('#')) continue;

            var stray = remainder[..(remainder.IndexOf('#') + 1)];
            var excerpt = text.Length > 120 ? text[..120] + "…" : text;
            yield return new VigilancePoint("note-stray-hash", "critical",
                "Caractère '#' superflu dans une note (BT-22)",
                $"La note « {excerpt} » contient un caractère '#' supplémentaire (« …{stray}… ») dans son texte libre, " +
                "après le préfixe de code sujet initial. La convention France_RFE encode le sujet de la note via un unique " +
                "préfixe \"#CODE#\" en début de texte : un '#' supplémentaire peut être interprété à tort comme le début d'un " +
                "nouveau segment de code par les plateformes de réception (Chorus Pro / PDP). Ce cas précis a déjà provoqué " +
                "des rejets de factures en production. Il faut retirer ou reformuler le texte libre pour qu'il ne contienne " +
                "plus de caractère '#'.");
        }
    }

    /// <summary>Pratiques valides en XML mais déconseillées par la réforme française (recommandations Chorus Pro / PPF).</summary>
    private static List<VigilancePoint> VigilancePoints(string? formatKey, XElement root)
    {
        var points = new List<VigilancePoint>();
        var prefix = root.GetPrefixOfNamespace(root.Name.Namespace);
        if (formatKey is not null && Rulesets.Formats[formatKey] is { ExpectsDefaultNamespace: true } format && !string.IsNullOrEmpty(prefix))
        {
            points.Add(new VigilancePoint("root-namespace-prefix", "warning",
                "Espace de noms racine préfixé au lieu du namespace par défaut",
                $"L'élément racine est déclaré <{prefix}:{root.Name.LocalName}> avec un préfixe d'espace de noms explicite (\"{prefix}:\") " +
                $"au lieu de l'espace de noms {format.Label} par défaut (xmlns=\"...\" sans préfixe). C'est valide en XML et n'affecte pas " +
                "la conformité Schematron, mais cela s'écarte des bonnes pratiques de la réforme de facturation électronique en France : " +
                "certains outils de lecture UBL (dont Sage) peuvent ne pas reconnaître le document. Il est recommandé de déclarer " +
                $"l'espace de noms {format.Label} comme namespace par défaut sur <{root.Name.LocalName}>."));
        }
        points.AddRange(StrayHashNotePoints(formatKey, root));
        return points;
    }

    // ----- Point d'entrée -----

    private static string CustomizationIdOf(string formatKey, XElement root)
    {
        var element = formatKey == "ubl"
            ? root.Descendants(Cbc + "CustomizationID").FirstOrDefault()
            : root.Descendants(Ram + "GuidelineSpecifiedDocumentContextParameter").Elements(Ram + "ID").FirstOrDefault();
        return element?.Value.Trim() ?? "";
    }

    /// <summary>Contrôle un document XML ; <paramref name="profileKey"/> null = détection automatique du profil.</summary>
    public static ConformityReport Validate(byte[] xmlBytes, string? profileKey)
    {
        XElement root;
        try
        {
            using var stream = new MemoryStream(xmlBytes);
            root = XDocument.Load(stream).Root ?? throw new XmlException("document vide");
        }
        catch (XmlException ex)
        {
            throw new ConformityException($"Le fichier n'est pas un XML valide : {ex.Message}");
        }

        var rootLocalName = root.Name.LocalName;
        var formatKey = Rulesets.FormatKeyOfRoot(rootLocalName);
        if (formatKey is null)
        {
            var supported = string.Join(", ", Rulesets.Formats.Values.SelectMany(f => f.RootLocalNames).Select(n => $"<{n}>"));
            throw new ConformityException($"Élément racine inattendu <{rootLocalName}>. Éléments racine reconnus : {supported}.");
        }

        var customizationId = CustomizationIdOf(formatKey, root);
        var detected = Rulesets.DetectProfile(formatKey, customizationId);
        var profile = profileKey is not null && Rulesets.Profiles.TryGetValue(profileKey, out var chosen) && chosen.FormatKey == formatKey
            ? chosen
            : Rulesets.Profiles[detected];

        var xsdPath = Rulesets.XsdPathFor(profile, rootLocalName);
        var xsd = xsdPath is null ? null : ValidateXsd(xsdPath, xmlBytes);

        var results = profile.SchematronSets.SelectMany(set => RunSchematronSet(set, xmlBytes))
            .OrderBy(r => r.Status == RuleResult.Failed ? 0 : r.Status == RuleResult.Passed ? 1 : 2)
            .ThenBy(r => r.Flag == "fatal" ? 0 : 1)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        return new ConformityReport
        {
            RootElement = rootLocalName,
            FormatLabel = Rulesets.Formats[formatKey].Label,
            CustomizationId = customizationId,
            DetectedProfile = detected,
            Profile = profile,
            VigilancePoints = VigilancePoints(formatKey, root),
            Xsd = xsd,
            Rules = results,
        };
    }
}
