using System.Net.Http.Json;
using System.Text.Json;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Lit l'API REST du serveur Azure DevOps. Chaque requête est un GET : aucun membre de cette classe ne peut
/// exprimer une modification, ce qui rend la garantie de lecture seule structurelle et non disciplinaire.
/// </summary>
/// <remarks>
/// Le pipeline de résilience ne signale pas tout par un <see cref="HttpRequestException"/> : un budget de
/// temps épuisé lève <see cref="TimeoutRejectedException"/> et un disjoncteur ouvert
/// <see cref="BrokenCircuitException"/>. Les attraper ici est ce qui garde la promesse de ce type, à savoir
/// qu'il rend un <see cref="Result"/> plutôt que de laisser une exception traverser toutes les couches
/// jusqu'à terminer le processus.
/// </remarks>
public sealed class AzureDevOpsApiReader
{
    /// <summary>L'en-tête de réponse au moyen duquel Azure DevOps pagine.</summary>
    public const string ContinuationTokenHeader = "x-ms-continuationtoken";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AzureDevOpsClient _client;
    private readonly AzureDevOpsServerOptions _options;
    private readonly ILogger<AzureDevOpsApiReader> _logger;

    /// <summary>Crée le lecteur.</summary>
    /// <param name="client">Le client typé.</param>
    /// <param name="options">Les options serveur validées.</param>
    /// <param name="logger">Reçoit l'issue des requêtes, jamais les corps de réponse.</param>
    public AzureDevOpsApiReader(
        AzureDevOpsClient client,
        IOptions<AzureDevOpsServerOptions> options,
        ILogger<AzureDevOpsApiReader> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>L'hôte auquel le lecteur s'adresse, pour l'affichage et les messages d'échec.</summary>
    public string ServerHost =>
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out Uri? uri) ? uri.Host : _options.BaseUrl;

    /// <summary>La collection à laquelle le lecteur s'adresse.</summary>
    public string Collection => _options.Collection;

    /// <summary>Émet un GET et désérialise le corps de la réponse.</summary>
    /// <typeparam name="T">Le type de corps attendu.</typeparam>
    /// <param name="relativeUrl">L'URL relative à l'adresse de base, segments déjà échappés.</param>
    /// <param name="cancellationToken">Annule la requête.</param>
    /// <returns>Le corps désérialisé, ou un échec déjà traduit.</returns>
    public async Task<Result<T>> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUrl);

        using HttpRequestMessage request = new(HttpMethod.Get, relativeUrl);

        try
        {
            using HttpResponseMessage response = await _client.HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.RequestFailed(_logger, (int)response.StatusCode);
                return Result.Fail<T>(
                    AzureDevOpsFailures.FromStatusCode(response.StatusCode, ServerHost, Collection));
            }

            T? body = await response.Content
                .ReadFromJsonAsync<T>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return body is null
                ? Result.Fail<T>(FailureReason.ServiceUnavailable, "The server returned an empty response.")
                : Result.Success(body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Le jeton de l'appelant n'a pas bougé : ce n'est donc pas le développeur qui a annulé, mais un
            // délai d'attente qui a expiré. Le dire ainsi ouvre l'invite de reprise au lieu de terminer
            // l'exécution en silence.
            return Result.Fail<T>(AzureDevOpsFailures.TimedOut());
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<T>(AzureDevOpsFailures.Cancelled());
        }
        catch (TimeoutRejectedException)
        {
            return Result.Fail<T>(AzureDevOpsFailures.TimedOut());
        }
        catch (BrokenCircuitException)
        {
            return Result.Fail<T>(AzureDevOpsFailures.ServiceUnavailable());
        }
        catch (HttpRequestException exception)
        {
            Log.RequestTransportFailure(_logger);
            return Result.Fail<T>(AzureDevOpsFailures.FromTransportException(exception));
        }
        catch (JsonException)
        {
            return Result.Fail<T>(
                FailureReason.ServiceUnavailable,
                "The server returned a response the tool could not understand.");
        }
    }

    /// <summary>Émet un GET pour une page de liste et renvoie son jeton de continuation.</summary>
    /// <typeparam name="T">Le type des éléments.</typeparam>
    /// <param name="relativeUrl">L'URL relative à l'adresse de base, segments déjà échappés.</param>
    /// <param name="cancellationToken">Annule la requête.</param>
    /// <returns>La page et son jeton de continuation, ou un échec déjà traduit.</returns>
    public async Task<Result<PagedReadResult<T>>> GetPageAsync<T>(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeUrl);

        using HttpRequestMessage request = new(HttpMethod.Get, relativeUrl);

        try
        {
            using HttpResponseMessage response = await _client.HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.RequestFailed(_logger, (int)response.StatusCode);
                return Result.Fail<PagedReadResult<T>>(
                    AzureDevOpsFailures.FromStatusCode(response.StatusCode, ServerHost, Collection));
            }

            ListResponse<T>? body = await response.Content
                .ReadFromJsonAsync<ListResponse<T>>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            string? continuationToken = ReadContinuationToken(response);

            return Result.Success(new PagedReadResult<T>(body?.Value ?? [], continuationToken));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Fail<PagedReadResult<T>>(AzureDevOpsFailures.TimedOut());
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<PagedReadResult<T>>(AzureDevOpsFailures.Cancelled());
        }
        catch (TimeoutRejectedException)
        {
            return Result.Fail<PagedReadResult<T>>(AzureDevOpsFailures.TimedOut());
        }
        catch (BrokenCircuitException)
        {
            return Result.Fail<PagedReadResult<T>>(AzureDevOpsFailures.ServiceUnavailable());
        }
        catch (HttpRequestException exception)
        {
            Log.RequestTransportFailure(_logger);
            return Result.Fail<PagedReadResult<T>>(AzureDevOpsFailures.FromTransportException(exception));
        }
        catch (JsonException)
        {
            return Result.Fail<PagedReadResult<T>>(
                FailureReason.ServiceUnavailable,
                "The server returned a response the tool could not understand.");
        }
    }

    /// <summary>
    /// Suit une liste paginée jusqu'au bout en accumulant tous les éléments. L'échec d'une page fait échouer
    /// la lecture entière, afin que l'appelant ne reçoive jamais une liste silencieusement tronquée.
    /// </summary>
    /// <typeparam name="T">Le type des éléments.</typeparam>
    /// <param name="buildUrl">
    /// Construit l'URL pour un jeton de continuation. Appelée avec <see langword="null"/> pour la première page.
    /// </param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Tous les éléments de toutes les pages, ou un échec déjà traduit.</returns>
    public async Task<Result<IReadOnlyList<T>>> GetAllPagesAsync<T>(
        Func<string?, string> buildUrl,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buildUrl);

        List<T> all = [];
        string? continuationToken = null;

        do
        {
            Result<PagedReadResult<T>> page = await GetPageAsync<T>(
                buildUrl(continuationToken), cancellationToken).ConfigureAwait(false);

            if (page.IsFailure)
            {
                return Result.Fail<IReadOnlyList<T>>(page.Failure!);
            }

            all.AddRange(page.Value.Items);
            continuationToken = page.Value.ContinuationToken;
        }
        while (!string.IsNullOrEmpty(continuationToken));

        return Result.Success<IReadOnlyList<T>>(all);
    }

    private static string? ReadContinuationToken(HttpResponseMessage response) =>
        response.Headers.TryGetValues(ContinuationTokenHeader, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;
}
