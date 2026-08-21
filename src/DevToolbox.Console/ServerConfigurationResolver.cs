using DevToolbox.Infrastructure.Platform;
using DevToolbox.Infrastructure.Storage;
using DevToolbox.Presentation.Shell;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace DevToolbox.Console;

/// <summary>
/// Détermine à quel serveur Azure DevOps la boîte à outils s'adresse, avant la construction de l'hôte.
/// </summary>
/// <remarks>
/// L'ordre de priorité est : ce que le développeur a confirmé et que nous avons enregistré, puis la
/// suggestion livrée dans <c>appsettings.json</c>. La valeur livrée n'est jamais traitée comme un réglage
/// d'installation à modifier : elle n'est que la proposition par défaut de l'invite du premier démarrage.
/// </remarks>
internal sealed class ServerConfigurationResolver
{
    private const string BaseUrlKey = "AzureDevOpsServer:BaseUrl";
    private const string CollectionKey = "AzureDevOpsServer:Collection";

    private readonly SettingsStore _settingsStore;
    private readonly FirstRunConfigurationPrompt _prompt;

    internal ServerConfigurationResolver(AppPaths paths, IAnsiConsole console)
    {
        _settingsStore = new SettingsStore(paths);
        _prompt = new FirstRunConfigurationPrompt(console);
    }

    /// <summary>Résout l'adresse du serveur, en interrogeant puis en enregistrant au premier démarrage.</summary>
    /// <param name="configuration">Fournit les suggestions livrées.</param>
    /// <param name="cancellationToken">Annule la lecture et l'écriture des réglages.</param>
    /// <returns>Les valeurs de configuration à superposer.</returns>
    /// <remarks>
    /// L'appelant a déjà établi que la session est interactive : cette méthode peut donc interroger librement.
    /// </remarks>
    internal async Task<IReadOnlyDictionary<string, string?>> ResolveAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        string hintBaseUrl = configuration[BaseUrlKey] ?? string.Empty;
        string hintCollection = configuration[CollectionKey] ?? "DefaultCollection";

        ToolboxSettings settings = await _settingsStore.LoadAsync(cancellationToken);

        if (IsUsable(settings.ServerBaseUrl) && IsUsable(settings.ServerCollection))
        {
            return Overrides(settings.ServerBaseUrl!, settings.ServerCollection!);
        }

        ServerConfigurationAnswer answer = _prompt.Ask(hintBaseUrl, hintCollection);

        settings.ServerBaseUrl = answer.BaseUrl;
        settings.ServerCollection = answer.Collection;
        await _settingsStore.SaveAsync(settings, cancellationToken);

        return Overrides(answer.BaseUrl, answer.Collection);
    }

    private static Dictionary<string, string?> Overrides(string baseUrl, string collection) =>
        new(StringComparer.Ordinal)
        {
            [BaseUrlKey] = baseUrl,
            [CollectionKey] = collection,
        };

    private static bool IsUsable(string? value) => !string.IsNullOrWhiteSpace(value);
}
