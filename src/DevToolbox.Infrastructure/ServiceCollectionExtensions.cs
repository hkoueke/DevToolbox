using DevToolbox.Application.Abstractions;
using DevToolbox.Infrastructure.Logging;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Infrastructure;

/// <summary>Enregistre les adaptateurs d'infrastructure partagés : horloge, chemins, stockage, journaux.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Enregistre les services d'infrastructure partagés.</summary>
    /// <param name="services">La collection de services.</param>
    /// <returns>La collection de services, pour le chaînage.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AppPaths>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<IRunCheckpointStore, FileRunCheckpointStore>();
        services.AddSingleton<RedactionRegistry>();

        return services;
    }

    /// <summary>
    /// Ajoute le puits de journalisation à rotation, enveloppé dans le décorateur de masquage, afin que le
    /// masquage soit imposé à la frontière du puits plutôt que confié aux appelants.
    /// </summary>
    /// <param name="builder">Le constructeur de journalisation.</param>
    /// <param name="configuration">La configuration de l'application.</param>
    /// <param name="paths">Résout et confine le dossier des journaux.</param>
    /// <param name="registry">L'ensemble des types qui ne doivent jamais atteindre un puits.</param>
    /// <returns>Le constructeur de journalisation, pour le chaînage.</returns>
    public static ILoggingBuilder AddRedactedFileLogging(
        this ILoggingBuilder builder,
        IConfiguration configuration,
        AppPaths paths,
        RedactionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(registry);

        FileLoggerOptions options = new();
        configuration.GetSection(FileLoggerOptions.SectionName).Bind(options);

        FileLoggerProvider fileProvider = new(paths, options);
        builder.AddProvider(new RedactingLoggerProvider(fileProvider, registry));

        return builder;
    }
}
