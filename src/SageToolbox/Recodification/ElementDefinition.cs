namespace SageToolbox.Recodification;

public enum ElementKind { ComptesGeneraux, Tiers, Journaux, Analytique, Familles, Articles }

/// <summary>Décrit un type d'élément recodifiable : table Sage, colonnes code/intitulé et script de recodification associé.</summary>
public sealed record ElementDefinition(
    ElementKind Kind,
    string Title,
    string Table,
    string KeyColumn,
    string LabelColumn,
    string? Filter,
    string? PlanColumn,
    string TemplateName,
    bool NeedsGescom,
    string? NatureColumn = null,
    bool SupportsMerge = false)
{
    public bool HasPlan => PlanColumn is not null;

    public override string ToString() => Title;

    public static readonly IReadOnlyList<ElementDefinition> All =
    [
        new(ElementKind.ComptesGeneraux, "Comptes généraux", "F_COMPTEG", "CG_Num", "CG_Intitule", "CG_Type = 0", null, "Pcg", false, "CG_Type", true),
        new(ElementKind.Tiers, "Comptes tiers", "F_COMPTET", "CT_Num", "CT_Intitule", null, null, "Tiers", false, "CT_Type", true),
        new(ElementKind.Journaux, "Journaux", "F_JOURNAUX", "JO_Num", "JO_Intitule", null, null, "Journaux", false),
        new(ElementKind.Analytique, "Comptes analytiques", "F_COMPTEA", "CA_Num", "CA_Intitule", "CA_Type = 0", "N_Analytique", "Analytique", false, "CA_Type", true),
        new(ElementKind.Familles, "Familles d'articles", "F_FAMILLE", "FA_CodeFamille", "FA_Intitule", "FA_Type = 0", null, "Familles", true),
        new(ElementKind.Articles, "Articles", "F_ARTICLE", "AR_Ref", "AR_Design", null, null, "Articles", true),
    ];
}
