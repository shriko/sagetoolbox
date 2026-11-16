
namespace SageToolbox.Recodification;

public sealed record ValidationSummary(int ToRecode, int Errors, int Merges = 0);

public static class RecodValidator
{
    public const string MergeMessage = "Ce compte existe déjà, il va être fusionné";

    /// <summary>
    /// Contrôle les nouveaux codes saisis et renseigne Error / Info / IsMerge sur chaque élément.
    /// <paramref name="existingKeys"/> : clés (plan, code, nature) de TOUTE la table, filtre de type compris.
    /// Si <paramref name="allowMerge"/>, un code déjà existant de même nature est une fusion ; sinon c'est une erreur.
    /// </summary>
    public static ValidationSummary Validate(
        IReadOnlyCollection<RecodItem> items,
        IReadOnlyCollection<(int Plan, string Code, int Nature)> existingKeys,
        int maxLength,
        int fixedLength,
        bool allowMerge = false)
    {
        var existing = new Dictionary<string, (string Code, int Nature)>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in existingKeys) existing[Key(k.Plan, k.Code)] = (k.Code, k.Nature);

        var changed = items.Where(i => i.IsChanged).ToList();
        var sources = changed.Select(i => Key(i.Plan, i.Code)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Doublons : seuls les nouveaux codes (non existants) saisis plusieurs fois posent problème ; plusieurs sources peuvent fusionner vers une même fiche.
        var duplicates = changed
            .Where(i => !existing.ContainsKey(Key(i.Plan, i.NewCodeTrimmed)))
            .GroupBy(i => Key(i.Plan, i.NewCodeTrimmed), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int toRecode = 0, errors = 0, merges = 0;
        foreach (var item in items)
        {
            item.Error = null;
            item.Info = null;
            item.IsMerge = false;
            item.MergeTarget = null;
            if (!item.IsChanged) continue;

            var code = item.NewCodeTrimmed;
            var destKey = Key(item.Plan, code);
            string? error = null;
            if (maxLength > 0 && code.Length > maxLength)
                error = $"Longueur maximale : {maxLength} caractères";
            else if (existing.TryGetValue(destKey, out var dest))
            {
                if (!allowMerge)
                    error = "Ce code existe déjà dans la base";
                else if (!existing.TryGetValue(Key(item.Plan, item.Code), out var source) || source.Nature != dest.Nature)
                    error = "Fusion impossible : les deux fiches ne sont pas de même nature";
                else if (sources.Contains(destKey))
                    error = "Fusion impossible : la fiche de destination est elle-même recodifiée";
                else
                {
                    item.IsMerge = true;
                    item.MergeTarget = dest.Code;
                    item.Info = MergeMessage;
                    merges++;
                }
            }
            else if (fixedLength > 0 && code.Length != fixedLength)
                error = $"Longueur imposée par le dossier : {fixedLength} caractères";
            else if (duplicates.Contains(destKey))
                error = "Code saisi plusieurs fois";

            item.Error = error;
            if (error is null) toRecode++; else errors++;
        }
        return new ValidationSummary(toRecode, errors, merges);
    }

    private static string Key(int plan, string code) => $"{plan}|{code}";
}
