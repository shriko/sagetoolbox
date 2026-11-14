using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using SageToolbox.Core;

namespace SageToolbox.Enrichissement;

public interface IApiClient
{
    Task<List<ApiCompanyResult>> SearchBySiretAsync(string siret, CancellationToken ct);
    Task<List<ApiCompanyResult>> SearchBySirenAsync(string siren, CancellationToken ct);
    Task<List<ApiCompanyResult>> SearchByNameAsync(string name, string? postalCode, CancellationToken ct);
}

/// <summary>
/// Client pour l'API "Recherche d'entreprises" (recherche-entreprises.api.gouv.fr).
/// Throttle global thread-safe (0,35 s min entre requêtes, partagé entre tous les workers) et
/// retry/backoff sur 429 (respecte Retry-After, sinon backoff exponentiel plafonné à 10 s, 5 tentatives max ;
/// un 429 repousse la fenêtre de disponibilité pour TOUS les threads).
/// </summary>
public sealed class ApiClient : IApiClient
{
    private const string BaseUrl = "https://recherche-entreprises.api.gouv.fr/search";
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(10);
    private const int MaxAttempts = 5;

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _throttleLock = new(1, 1);
    private DateTime _nextAllowedUtc = DateTime.MinValue;

    public ApiClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }

    public Task<List<ApiCompanyResult>> SearchBySiretAsync(string siret, CancellationToken ct) =>
        SearchAsync(siret, null, ct);

    public Task<List<ApiCompanyResult>> SearchBySirenAsync(string siren, CancellationToken ct) =>
        SearchAsync(siren, null, ct);

    public Task<List<ApiCompanyResult>> SearchByNameAsync(string name, string? postalCode, CancellationToken ct) =>
        SearchAsync(name, postalCode, ct);

    private async Task<List<ApiCompanyResult>> SearchAsync(string query, string? postalCode, CancellationToken ct)
    {
        var queryString = new StringBuilder();
        queryString.Append("q=").Append(Uri.EscapeDataString(query));
        if (!string.IsNullOrEmpty(postalCode) && postalCode.Length >= 5)
        {
            queryString.Append("&code_postal=").Append(Uri.EscapeDataString(postalCode));
        }
        string url = $"{BaseUrl}?{queryString}";

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await WaitForThrottleAsync(ct).ConfigureAwait(false);

            try
            {
                using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    TimeSpan delay = GetRetryDelay(response, attempt);
                    Logger.Instance.Warning($"Enrichissement tiers : API 429 (limite de débit) reçu, tentative {attempt}/{MaxAttempts}, attente {delay.TotalSeconds:0.#} s.");
                    PushBackAvailability(delay);
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Instance.Warning($"Enrichissement tiers : API erreur HTTP {(int)response.StatusCode} pour la requête '{query}'.");
                    throw new ApiConnectionException($"L'API a répondu {(int)response.StatusCode} pour la requête '{query}'.");
                }

                var body = await response.Content.ReadFromJsonAsync<ApiSearchResponse>(cancellationToken: ct).ConfigureAwait(false);
                return body?.Results ?? new List<ApiCompanyResult>();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (ApiConnectionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"Enrichissement tiers : erreur réseau lors de l'appel API pour '{query}' ({ex.Message}).");
                // Incident réseau/timeout : on ne peut pas savoir si le tiers existe ou non dans le
                // registre. Ne jamais renvoyer une liste vide ici, ce qui serait interprété comme
                // "confirmé absent" (Non trouvé) alors qu'il s'agit d'une simple incertitude.
                throw new ApiConnectionException($"Impossible de contacter l'API pour la requête '{query}'.", ex);
            }
        }

        Logger.Instance.Error($"Enrichissement tiers : API, nombre maximum de tentatives ({MaxAttempts}) atteint après 429 répétés pour '{query}'.");
        throw new ApiConnectionException($"Limite de débit (429) API atteinte après {MaxAttempts} tentatives pour '{query}'.");
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta) return delta;
        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero) return remaining;
        }

        double backoffSeconds = Math.Min(Math.Pow(2, attempt), MaxBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(backoffSeconds);
    }

    /// <summary>Repousse la fenêtre de disponibilité globale pour tous les threads (évite une vague de 429).</summary>
    private void PushBackAvailability(TimeSpan delay)
    {
        var candidate = DateTime.UtcNow + delay;
        if (candidate > _nextAllowedUtc) _nextAllowedUtc = candidate;
    }

    private async Task WaitForThrottleAsync(CancellationToken ct)
    {
        await _throttleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            var waitUntil = _nextAllowedUtc > now ? _nextAllowedUtc : now;
            var wait = waitUntil - now;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }
            _nextAllowedUtc = DateTime.UtcNow + MinInterval;
        }
        finally
        {
            _throttleLock.Release();
        }
    }
}
