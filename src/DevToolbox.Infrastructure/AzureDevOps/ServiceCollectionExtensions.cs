using DevToolbox.Domain.AzureDevOps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<IValidateOptions<AzureDevOpsServerOptions>>(
            new AzureDevOpsServerOptionsValidator(settingsPath));

        services
            .AddOptions<ResilienceOptions>()
            .Bind(configuration.GetSection(ResilienceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        ResilienceOptions resilience = new();
        configuration.GetSection(ResilienceOptions.SectionName).Bind(resilience);

        services
            .AddHttpClient<AzureDevOpsClient>(ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(IntegratedAuthHandlerFactory.Create)
            .AddStandardResilienceHandler(options =>
            {
                // Déclaré une seule fois, à l'enregistrement, plutôt que dispersé sur les sites d'appel.
                // Le gestionnaire standard applique déjà un recul exponentiel AVEC part d'aléa et respecte
                // l'en-tête Retry-After : aucune stratégie sur mesure n'est nécessaire.
                options.Retry.MaxRetryAttempts = resilience.MaxRetryAttempts;
                options.Retry.Delay = TimeSpan.FromMilliseconds(resilience.RetryBaseDelayMilliseconds);

                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(resilience.AttemptTimeoutSeconds);
                options.TotalRequestTimeout.Timeout =
                    TimeSpan.FromSeconds(resilience.TotalRequestTimeoutSeconds);

                options.CircuitBreaker.SamplingDuration =
                    TimeSpan.FromSeconds(resilience.CircuitBreakerSamplingDurationSeconds);
                options.CircuitBreaker.FailureRatio = resilience.CircuitBreakerFailureRatio;
                options.CircuitBreaker.MinimumThroughput = resilience.CircuitBreakerMinimumThroughput;

                // Noter ce qui n'y figure PAS : DisableForUnsafeHttpMethods(). Chaque requête de cet outil
                // est un GET, si bien que la sémantique complète de reprise est sûre par construction et
                // non par configuration.
            });

        services.AddSingleton<AzureDevOpsApiReader>();

        return services;
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
