namespace SageToolbox.Schematron;

/// <summary>Une paire fichier .sch (catalogue des règles) / fichier .xslt compilé (moteur d'exécution).</summary>
internal sealed record SchematronSet(string Key, string Label, string SchPath, string XsltPath);

public sealed record Profile(string Key, string Label, string Description, string FormatKey)
{
    internal IReadOnlyList<SchematronSet> SchematronSets { get; init; } = [];
}

internal sealed record DocumentFormat(string Key, string Label, string[] RootLocalNames, bool ExpectsDefaultNamespace);

/// <summary>
/// Jeux de règles Schematron disponibles par format de document électronique (UBL, CII,
/// Factur-X, CDAR) et par profil de conformité.
/// </summary>
public static class Rulesets
{
    public const string RamNamespace = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    public const string CbcNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>Version des normes sur lesquelles sont basés les schematrons embarqués.</summary>
    public const string StandardVersionLabel =
        "EN16931:2017 (CEN/TC 434, schématron v1.3.16) · France_RFE 1.4.0.04 (AFNOR XP Z12-012 v1.4) · UBL 2.1 / UN-CEFACT CII D22B";

    /// <summary>Jeu de règles d'un dossier de profil ; le XSLT porte le nom du .sch sauf si <paramref name="xsltName"/> est précisé.</summary>
    private static SchematronSet Core(string key, string label, string dir, string name, string? xsltName = null) =>
        new(key, label, $"{dir}/schematron/{name}.sch", $"{dir}/2xslt/{xsltName ?? name}.xslt");

    /// <summary>Règles France Flux2 (erreurs bloquantes puis avertissements) d'un dossier de profil.</summary>
    private static SchematronSet[] FrFlux2(string dir, string name) =>
    [
        Core("fr-flux2", "Règles France Flux2 (erreurs bloquantes)", dir, name),
        Core("fr-flux2-warning", "Règles France Flux2 (avertissements)", dir, name + "_WARNING"),
    ];

    private static Profile Make(string key, string label, string description, string formatKey, params SchematronSet[] sets) =>
        new(key, label, description, formatKey) { SchematronSets = sets };

    public static IReadOnlyDictionary<string, Profile> Profiles { get; } = BuildProfiles();

    private static Dictionary<string, Profile> BuildProfiles()
    {
        const string extendedDescription =
            "Profil français étendu (inclut l'intégralité des règles EN16931) pour la réforme de la " +
            "facturation électronique en France";

        var list = new[]
        {
            Make("ubl-en16931", "UBL — EN16931 (norme européenne)",
                "Règles de la norme européenne de facturation électronique EN 16931, liées au format UBL.", "ubl",
                [Core("en16931-core", "EN16931 - modèle sémantique", "UBL/EN16931", "EN16931-UBL-validation-preprocessed", "EN16931-UBL-validation"),
                 .. FrFlux2("UBL/EN16931", "BR-FR-Flux2-Schematron-UBL")]),
            Make("ubl-extended-ctc-fr", "UBL — EXTENDED-CTC-FR (réforme e-invoicing France)",
                extendedDescription + " (Plateformes Agréées / PPF).", "ubl",
                [Core("extended-ctc-fr-core", "EXTENDED-CTC-FR - modèle sémantique (inclut EN16931)", "UBL/EXTENDED-CTC-FR", "EXTENDED-CTC-FR-UBL"),
                 .. FrFlux2("UBL/EXTENDED-CTC-FR", "BR-FR-Flux2-Schematron-UBL")]),
            Make("cii-en16931", "CII — EN16931 (norme européenne)",
                "Règles EN 16931 liées au format UN/CEFACT CII (CrossIndustryInvoice).", "cii",
                [Core("en16931-core", "EN16931 - modèle sémantique", "CII/EN16931", "EN16931-CII-validation-preprocessed", "EN16931-CII-validation"),
                 .. FrFlux2("CII/EN16931", "BR-FR-Flux2-Schematron-CII")]),
            Make("cii-extended-ctc-fr", "CII — EXTENDED-CTC-FR (réforme e-invoicing France)",
                extendedDescription + ", au format UN/CEFACT CII.", "cii",
                [Core("extended-ctc-fr-core", "EXTENDED-CTC-FR - modèle sémantique (inclut EN16931)", "CII/EXTENDED-CTC-FR", "EXTENDED-CTC-FR-CII"),
                 .. FrFlux2("CII/EXTENDED-CTC-FR", "BR-FR-Flux2-Schematron-CII")]),
            Make("facturx-en16931", "Factur-X — EN16931",
                "Profil Factur-X EN16931 (hybride PDF/CII) — contrôle de la partie XML (CII).", "cii",
                [Core("facturx-en16931-core", "Factur-X EN16931 - modèle sémantique", "Factur-X/EN16931", "FACTUR-X_EN16931"),
                 .. FrFlux2("Factur-X/EN16931", "BR-FR-Flux2-Schematron-CII")]),
            Make("facturx-extended", "Factur-X — EXTENDED",
                "Profil Factur-X EXTENDED (hybride PDF/CII) — contrôle de la partie XML (CII).", "cii",
                [Core("facturx-extended-core", "Factur-X EXTENDED - modèle sémantique", "Factur-X/EXTENDED", "FACTUR-X_EXTENDED"),
                 .. FrFlux2("Factur-X/EXTENDED", "BR-FR-Flux2-Schematron-CII")]),
            Make("facturx-basicwl", "Factur-X — BASIC WL",
                "Profil Factur-X BASIC WL (hybride PDF/CII, sans lignes) — contrôle de la partie XML (CII).", "cii",
                [Core("facturx-basicwl-core", "Factur-X BASIC WL - modèle sémantique", "Factur-X/BASICWL", "FACTUR-X_BASIC-WL"),
                 .. FrFlux2("Factur-X/BASICWL", "BR-FR-Flux2-Schematron-CII")]),
            Make("cdar", "CDAR — Cycle de vie (accusés/statuts)",
                "Règles applicables aux messages d'accusé de réception et de statut de cycle de vie " +
                "(CrossDomainAcknowledgementAndResponse) de la réforme de facturation électronique en France.", "cdar",
                [Core("cdv-core", "Règles France CDV (erreurs bloquantes)", "CDAR", "BR-FR-CDV-Schematron-CDAR"),
                 Core("cdv-warning", "Règles France CDV (avertissements)", "CDAR", "BR-FR-CDV-Schematron-CDAR_WARNING")]),
        };
        return list.ToDictionary(p => p.Key);
    }

    internal static IReadOnlyDictionary<string, DocumentFormat> Formats { get; } = new Dictionary<string, DocumentFormat>
    {
        ["ubl"] = new("ubl", "UBL", ["Invoice", "CreditNote"], ExpectsDefaultNamespace: true),
        ["cii"] = new("cii", "CII / Factur-X", ["CrossIndustryInvoice"], ExpectsDefaultNamespace: false),
        ["cdar"] = new("cdar", "CDAR", ["CrossDomainAcknowledgementAndResponse"], ExpectsDefaultNamespace: false),
    };

    /// <summary>Libellé du format d'un profil (utilisé pour grouper la liste des profils).</summary>
    public static string FormatLabel(Profile profile) => Formats[profile.FormatKey].Label;

    internal static string? FormatKeyOfRoot(string rootLocalName) =>
        Formats.Values.FirstOrDefault(f => f.RootLocalNames.Contains(rootLocalName))?.Key;

    private static readonly Dictionary<string, string> FacturXFolders = new()
    {
        ["facturx-en16931"] = "EN16931",
        ["facturx-extended"] = "EXTENDED",
        ["facturx-basicwl"] = "BASICWL",
    };

    /// <summary>Schéma XSD (structure) applicable à un profil, ou null s'il n'y en a pas.</summary>
    internal static string? XsdPathFor(Profile profile, string rootLocalName)
    {
        if (profile.FormatKey == "ubl") return $"UBL/1xsd_UBL2.1/maindoc/UBL-{rootLocalName}-2.1.xsd";
        if (FacturXFolders.TryGetValue(profile.Key, out var folder)) return $"Factur-X/{folder}/1xsd/Factur-X_{folder}.xsd";
        if (profile.FormatKey == "cii") return "CII/1xsd-CII_D22B_uncoupled/CrossIndustryInvoice_100pD22B.xsd";
        if (profile.FormatKey == "cdar") return "CDAR/1xsd-CDAR_D22B_uncoupled/CrossDomainAcknowledgementAndResponse_100pD22B.xsd";
        return null;
    }

    /// <summary>Devine le profil le plus probable à partir de l'équivalent BT-24 du document.</summary>
    internal static string DetectProfile(string formatKey, string customizationId)
    {
        var cid = (customizationId ?? "").ToLowerInvariant();
        switch (formatKey)
        {
            case "ubl":
                return cid.Contains("extended-ctc-fr") ? "ubl-extended-ctc-fr" : "ubl-en16931";
            case "cii":
                if (cid.Contains("factur-x.eu:1p0:basicwl")) return "facturx-basicwl";
                if (cid.Contains("factur-x.eu:1p0:extended")) return "facturx-extended";
                if (cid.Contains("extended-ctc-fr")) return "cii-extended-ctc-fr";
                if (cid.Contains("factur-x.eu")) return "facturx-en16931";
                return "cii-en16931";
            case "cdar":
                return "cdar";
            default:
                throw new ArgumentException($"Format inconnu : {formatKey}");
        }
    }
}
