using System.Net;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Enregistre le client Azure DevOps partagé. Il vit dans la couche d'infrastructure commune et non dans un
/// outil, afin que le second outil hérite des options, du client typé, du gestionnaire d'authentification
/// intégrée et du pipeline de résilience au lieu de les dupliquer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Le séparateur de segments d'URL. Nommé pour se lire comme une affaire d'URL, pas de chemin de fichier.</summary>
    private const char UrlSegmentSeparator = '/';

    /// <summary>Enregistre le client Azure DevOps et ses options.</summary>
    /// <param name="services">La collection de services.</param>
    /// <param name="configuration">La configuration de l'application.</param>
    /// <param name="settingsPath">Le fichier de réglages à nommer dans les messages d'échec de validation.</param>
    /// <returns>La collection de services, pour le chaînage.</returns>
    public static IServiceCollection AddAzureDevOps(
        this IServiceCollection services,
        IConfiguration configuration,
        string settingsPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AzureDevOpsServerOptions>()
            .Bind(configuration.GetSection(AzureDevOpsServerOptions.SectionName))
            .PostConfigure(NormaliseBaseUrl)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        ResilienceOptions resilience = new();
        configuration.GetSection(ResilienceOptions.SectionName).Bind(resilience);

        services.AddSingleton<IValidateOptions<AzureDevOpsServerOptions>>(
            new AzureDevOpsServerOptionsValidator(settingsPath, resilience.TotalRequestTimeoutSeconds));

        services
            .AddOptions<ResilienceOptions>()
            .Bind(configuration.GetSection(ResilienceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Là où rien n'écoute, le pipeline se construit quand même. Une couche de présentation qui veut
        // annoncer les reprises s'enregistre AVANT cet appel, et c'est elle qui l'emporte.
        services.TryAddSingleton<IRetryObserver, NullRetryObserver>();

        services
            .AddHttpClient<AzureDevOpsClient>(ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(IntegratedAuthHandlerFactory.Create)
            .AddStandardResilienceHandler()
            .Configure((options, provider) => ConfigureResilience(options, resilience, provider));

        services.AddSingleton<AzureDevOpsApiReader>();

        return services;
    }

    /// <summary>
    /// Configure le pipeline. Déclaré une seule fois, à l'enregistrement, plutôt que dispersé sur les sites
    /// d'appel. Le gestionnaire standard applique déjà un recul exponentiel AVEC part d'aléa et respecte
    /// l'en-tête Retry-After : aucune stratégie sur mesure n'est nécessaire.
    /// </summary>
    /// <remarks>
    /// Noter ce qui n'y figure PAS : <c>DisableForUnsafeHttpMethods()</c>. Chaque requête de cet outil est
    /// un GET, si bien que la sémantique complète de reprise est sûre par construction et non par
    /// configuration.
    /// </remarks>
    private static void ConfigureResilience(
        HttpStandardResilienceOptions options,
        ResilienceOptions resilience,
        IServiceProvider provider)
    {
        options.Retry.MaxRetryAttempts = resilience.MaxRetryAttempts;
        options.Retry.Delay = TimeSpan.FromMilliseconds(resilience.RetryBaseDelayMilliseconds);

        // Explicite, bien que ce soit déjà la valeur par défaut : patienter le temps que le serveur demande
        // est une promesse faite au développeur, pas un détail d'implémentation qu'une mise à jour de paquet
        // pourrait retourner en silence.
        options.Retry.ShouldRetryAfterHeader = true;

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(resilience.AttemptTimeoutSeconds);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(resilience.TotalRequestTimeoutSeconds);

        options.CircuitBreaker.SamplingDuration =
            TimeSpan.FromSeconds(resilience.CircuitBreakerSamplingDurationSeconds);
        options.CircuitBreaker.FailureRatio = resilience.CircuitBreakerFailureRatio;
        options.CircuitBreaker.MinimumThroughput = resilience.CircuitBreakerMinimumThroughput;

        AnnounceRetries(options, resilience, provider.GetRequiredService<IRetryObserver>());
    }

    /// <summary>
    /// Fait dire au pipeline qu'il rejoue. Sans cela, une reprise interne est indiscernable d'un blocage :
    /// l'écran ne bouge plus pendant tout le budget de temps, et le développeur conclut à une panne.
    /// </summary>
    private static void AnnounceRetries(
        HttpStandardResilienceOptions options,
        ResilienceOptions resilience,
        IRetryObserver observer)
    {
        options.Retry.OnRetry = arguments =>
        {
            HttpResponseMessage? response = arguments.Outcome.Result;

            // Un 503 tout seul ne dit pas que le débit est limité : il couvre aussi bien une surcharge, un
            // arrière-plan indisponible ou une maintenance. Annoncer une limitation de débit dans ce cas
            // enverrait le développeur patienter là où il devrait aller voir la santé du service. Seuls un
            // Retry-After explicite ou un 429, qui est une limitation par définition, la constatent.
            bool throttled = response is not null
                && (response.Headers.RetryAfter is not null
                    || response.StatusCode is HttpStatusCode.TooManyRequests);

            if (throttled)
            {
                observer.Throttled(arguments.RetryDelay);
            }
            else
            {
                // AttemptNumber compte les tentatives à partir de zéro ; le développeur compte les reprises
                // à partir de un.
                observer.Retrying(
                    arguments.AttemptNumber + 1, resilience.MaxRetryAttempts, arguments.RetryDelay);
            }

            return default;
        };
    }

    /// <summary>
    /// Complète le schéma sous-entendu d'une adresse de base avant que quoi que ce soit ne la valide ou ne
    /// s'y lie. Un serveur interne se désigne par son nom court, et la configuration doit accepter cette
    /// forme d'où qu'elle vienne : réglages enregistrés, fichier livré, variable d'environnement.
    /// </summary>
    /// <remarks>
    /// Une adresse qui ne se lit pas est laissée intacte : la dire mal formée revient au validateur, dont
    /// c'est le rôle et qui sait nommer le réglage fautif.
    /// </remarks>
    private static void NormaliseBaseUrl(AzureDevOpsServerOptions options)
    {
        if (ServerAddress.TryNormalise(
                options.BaseUrl, options.AllowInsecureHttp, out Uri? address, out _))
        {
            options.BaseUrl = address!.AbsoluteUri;
        }
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client)
    {
        AzureDevOpsServerOptions options =
            provider.GetRequiredService<IOptions<AzureDevOpsServerOptions>>().Value;

        // Un séparateur final est indispensable pour que les URL relatives se résolvent par rapport au
        // chemin de base complet au lieu d'en remplacer le dernier segment. Les installations situées
        // derrière un répertoire virtuel tel que /tfs en dépendent.
        string baseUrl = options.BaseUrl.EndsWith(UrlSegmentSeparator)
            ? options.BaseUrl
            : options.BaseUrl + UrlSegmentSeparator;

        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
    }
}
