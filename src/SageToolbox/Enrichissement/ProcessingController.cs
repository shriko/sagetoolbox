using System.Runtime.CompilerServices;

namespace SageToolbox.Enrichissement;

/// <summary>
/// Orchestre le traitement concurrent des tiers : pool de 5 workers, soumission par lots de 5
/// (pour que Stop prenne effet en ~1 lot), annulation coopérative sans tuer les requêtes en vol.
/// </summary>
public sealed class ProcessingController
{
    private const int MaxConcurrency = 5;
    private const int BatchSize = 5;

    private readonly ReconciliationEngine _engine;

    public ProcessingController(ReconciliationEngine engine)
    {
        _engine = engine;
    }

    /// <param name="tiers">Tiers à traiter.</param>
    /// <param name="onResult">Appelé pour chaque résultat obtenu (peut être invoqué depuis plusieurs threads).</param>
    /// <param name="onProgress">Appelé après chaque tiers traité avec (nombre traité, total).</param>
    /// <param name="isStopRequested">Vérifié entre chaque lot : si vrai, aucun nouveau lot n'est démarré.</param>
    /// <param name="ct">Annulation "dure" (fermeture de l'application), n'interrompt pas un lot en cours.</param>
    public async Task RunAsync(
        IReadOnlyList<TierRecord> tiers,
        Action<ResultRow> onResult,
        Action<int, int> onProgress,
        Func<bool> isStopRequested,
        CancellationToken ct = default)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var processed = new StrongBox<int>(0);
        int total = tiers.Count;

        for (int i = 0; i < tiers.Count; i += BatchSize)
        {
            if (isStopRequested() || ct.IsCancellationRequested) break;

            var batch = tiers.Skip(i).Take(BatchSize).ToList();
            var tasks = batch.Select(tier => ProcessOneAsync(tier, semaphore, onResult, onProgress, processed, total, ct));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(
        TierRecord tier,
        SemaphoreSlim semaphore,
        Action<ResultRow> onResult,
        Action<int, int> onProgress,
        StrongBox<int> processed,
        int total,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await _engine.ProcessTierAsync(tier, ct).ConfigureAwait(false);
            onResult(result);
        }
        finally
        {
            semaphore.Release();
            int done = Interlocked.Increment(ref processed.Value);
            onProgress(done, total);
        }
    }
}
