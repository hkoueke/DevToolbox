using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Infrastructure.Storage;
using DevToolbox.Presentation.Shell;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace DevToolbox.Console;

/// <summary>
/// Détermine à quel serveur Azure DevOps la boîte à outils s'adresse.
/// </summary>
/// <remarks>
/// L'ordre de priorité est : ce que le développeur a confirmé et que nous avons enregistré, puis la
/// suggestion livrée dans <c>appsettings.json</c>. La valeur livrée n'est jamais traitée comme un réglage
/// d'installation à modifier : elle n'est que la proposition par défaut de l'invite.
/// <para>
/// La résolution se fait en deux temps, et c'est ce qui compte ici. Au démarrage, seule l'adresse
/// <em>déjà enregistrée</em> est relue, sans rien demander. L'invite n'apparaît qu'au moment où un outil
/// qui a besoin d'un serveur est choisi. Un outil hors ligne — apidelta lit deux fichiers locaux — peut
/// ainsi s'utiliser sur un poste qui n'a jamais vu de serveur Azure DevOps, au lieu d'exiger qu'on en
/// invente l'adresse pour atteindre son onglet.
/// </para>
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

    /// <summary>Relit l'adresse déjà enregistrée, sans rien demander.</summary>
    /// <param name="cancellationToken">Annule la lecture des réglages.</param>
    /// <returns>
    /// Les valeurs à superposer à la configuration, ou <see langword="null"/> si le développeur n'a encore
    /// confirmé aucune adresse.
    /// </returns>
    internal async Task<IReadOnlyDictionary<string, string?>?> TryLoadStoredOverridesAsync(
        CancellationToken cancellationToken)
    {
        ToolboxSettings settings = await _settingsStore.LoadAsync(cancellationToken);

        if (!IsUsable(settings.ServerBaseUrl) || !IsUsable(settings.ServerCollection))
        {
            return null;
        }

        return Overrides(settings.ServerBaseUrl!, settings.ServerCollection!);
    }

    /// <summary>Demande l'adresse du serveur, puis l'enregistre.</summary>
    /// <param name="configuration">Fournit les suggestions livrées.</param>
    /// <param name="cancellationToken">Annule l'écriture des réglages.</param>
    /// <returns>Une tâche qui s'achève quand l'adresse est enregistrée.</returns>
    /// <remarks>
    /// L'appelant a déjà établi que la session est interactive : cette méthode peut donc interroger
    /// librement. Ce qu'elle ne fait pas, c'est modifier l'hôte en cours : les options d'un hôte construit
    /// sont liées, et un outil déjà instancié porte la cible qu'il a reçue. L'appelant reconstruit donc
    /// l'hôte, ce qui coûte une construction dans un chemin qui n'arrive qu'une fois.
    /// </remarks>
    internal async Task PromptAndStoreAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string hintBaseUrl = configuration[BaseUrlKey] ?? string.Empty;
        string hintCollection = configuration[CollectionKey] ?? "DefaultCollection";

        ServerConfigurationAnswer answer = _prompt.Ask(hintBaseUrl, hintCollection);

        ToolboxSettings settings = await _settingsStore.LoadAsync(cancellationToken);
        settings.ServerBaseUrl = answer.BaseUrl;
        settings.ServerCollection = answer.Collection;
        await _settingsStore.SaveAsync(settings, cancellationToken);
    }

    /// <summary>Superpose l'adresse résolue, normalisée au passage.</summary>
    private static Dictionary<string, string?> Overrides(string baseUrl, string collection) =>
        new(StringComparer.Ordinal)
        {
            [BaseUrlKey] = Normalise(baseUrl),
            [CollectionKey] = collection,
        };

    /// <summary>
    /// Normalise l'adresse. Une valeur enregistrée sous la forme courte par laquelle un serveur se désigne
    /// en interne — <c>azure</c> — est ainsi utilisable telle quelle par tout ce qui s'y lie ensuite.
    /// </summary>
    private static string Normalise(string baseUrl) =>
        ServerAddress.TryNormalise(baseUrl, allowInsecureHttp: true, out Uri? address, out _)
            ? address!.AbsoluteUri
            : baseUrl;

    private static bool IsUsable(string? value) => !string.IsNullOrWhiteSpace(value);
}
