using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Infrastructure;
using DevToolbox.Infrastructure.AzureDevOps;
using DevToolbox.Infrastructure.Logging;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core;
using DevToolbox.Tools.VarCompare.Infrastructure;
using DevToolbox.Tools.VarCompare.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace DevToolbox.Console;

/// <summary>
/// La racine de composition : le seul endroit autorisé à connaître toutes les couches.
/// </summary>
internal static class Program
{
    private const string SettingsFileName = "appsettings.json";

    /// <summary>Combien de temps une exécution interrompue est mémorisée avant d'être nettoyée.</summary>
    private static readonly TimeSpan RunRetention = TimeSpan.FromDays(7);

    private static async Task<int> Main(string[] args)
    {
        using CancellationHook cancellation = new();
        cancellation.Install();

        IAnsiConsole console = AnsiConsole.Console;

        // DevToolbox se pilote par menus. Établir une fois pour toutes, avant tout le reste, que cette
        // session peut répondre à une invite. Sinon, le dire et sortir plutôt que de rester bloqué sur une
        // question sans réponse possible.
        ConsoleCapabilities capabilities = new(console);

        if (!capabilities.IsInteractive)
        {
            return capabilities.ReportNonInteractiveAndExit();
        }

        try
        {
            using IHost host = await BuildHostAsync(args, console, cancellation.Token);
            return await RunAsync(host, console, cancellation.Token);
        }
        catch (FileNotFoundException)
        {
            // Le fichier de réglages voyage avec le binaire. S'il manque, l'installation est cassée, et le
            // dire simplement vaut mieux qu'une trace d'appel.
            console.MarkupLine(
                "[red]DevToolbox ne peut pas démarrer : " + SettingsFileName + " est absent de "
                + Markup.Escape(AppContext.BaseDirectory) + ".[/]");

            return ExitCodes.InvalidConfiguration;
        }
        catch (OperationCanceledException)
        {
            console.MarkupLine("[yellow]Annulé.[/]");
            return ExitCodes.Cancelled;
        }
    }

    private static async Task<IHost> BuildHostAsync(
        string[] args,
        IAnsiConsole console,
        CancellationToken cancellationToken)
    {
        // La racine de contenu est le dossier du binaire, pas le répertoire courant de l'appelant. Un outil
        // console se lance depuis n'importe où, et ses réglages l'accompagnent.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Configuration en couches : valeurs par défaut, puis fichier, puis environnement, puis arguments.
        // CreateApplicationBuilder ajoute déjà les trois dernières.
        builder.Configuration.AddJsonFile(SettingsFileName, optional: false, reloadOnChange: false);

        AppPaths paths = new();
        paths.EnsureCreated();

        // L'adresse du serveur provient de ce que le développeur a confirmé au premier démarrage, et non du
        // fichier livré. Elle est donc résolue et superposée avant que quoi que ce soit ne s'y lie.
        ServerConfigurationResolver resolver = new(paths, console);
        IReadOnlyDictionary<string, string?> serverOverrides =
            await resolver.ResolveAsync(builder.Configuration, cancellationToken);

        builder.Configuration.AddInMemoryCollection(serverOverrides);

        RedactionRegistry redaction = new();

        ConfigureLogging(builder.Logging, builder.Configuration, paths, redaction);
        ConfigureServices(builder.Services, builder.Configuration, paths, redaction, console);

        return builder.Build();
    }

    private static async Task<int> RunAsync(
        IHost host,
        IAnsiConsole console,
        CancellationToken cancellationToken)
    {
        try
        {
            AzureDevOpsServerOptions options = host.Services
                .GetRequiredService<IOptions<AzureDevOpsServerOptions>>().Value;

            // Les exécutions interrompues périmées sont nettoyées au démarrage, avant toute proposition.
            IRunCheckpointStore checkpoints = host.Services.GetRequiredService<IRunCheckpointStore>();
            await checkpoints.SweepExpiredAsync(RunRetention, cancellationToken);

            return await RunShellAsync(host.Services, console, options, cancellationToken);
        }
        catch (OptionsValidationException exception)
        {
            return ReportInvalidConfiguration(console, exception);
        }
    }

    private static int ReportInvalidConfiguration(IAnsiConsole console, OptionsValidationException exception)
    {
        // Une adresse enregistrée qui ne passe plus la validation : nommer le réglage fautif plutôt que
        // d'échouer obscurément.
        console.MarkupLine(
            "[red]DevToolbox ne peut pas démarrer : sa configuration de serveur n'est pas valide.[/]");

        foreach (string failure in exception.Failures)
        {
            console.MarkupLine("  [red]•[/] " + Markup.Escape(failure));
        }

        console.MarkupLine(
            "[grey]Supprimez l'adresse enregistrée pour qu'elle vous soit redemandée, ou corrigez-la dans "
            + "le fichier de réglages.[/]");

        return ExitCodes.InvalidConfiguration;
    }

    private static void ConfigureLogging(
        ILoggingBuilder logging,
        ConfigurationManager configuration,
        AppPaths paths,
        RedactionRegistry redaction)
    {
        // Le fournisseur console est volontairement retiré : la sortie de journal ne doit jamais aller sur la
        // sortie standard tant que Spectre occupe le terminal.
        logging.ClearProviders();
        logging.AddConfiguration(configuration.GetSection("Logging"));
        logging.AddRedactedFileLogging(configuration, paths, redaction);
    }

    /// <summary>
    /// Enregistre tous les services dont la boîte à outils a besoin. Interne plutôt que privé afin qu'un
    /// test puisse résoudre le graphe réel : un enregistrement manquant est une panne à l'exécution qu'aucun
    /// test unitaire ne rattraperait autrement.
    /// </summary>
    /// <param name="services">La collection de services.</param>
    /// <param name="configuration">La configuration de l'application.</param>
    /// <param name="paths">Les chemins d'application résolus.</param>
    /// <param name="redaction">Le registre de masquage du puits de journalisation.</param>
    /// <param name="console">La console de rendu.</param>
    internal static void ConfigureServices(
        IServiceCollection services,
        ConfigurationManager configuration,
        AppPaths paths,
        RedactionRegistry redaction,
        IAnsiConsole console)
    {
        services.AddSingleton(paths);
        services.AddSingleton(redaction);
        services.AddSingleton(console);

        services.AddInfrastructureServices();
        services.AddAzureDevOps(configuration, SettingsFileName);

        services.AddSingleton<ConsoleCapabilities>();
        services.AddSingleton<ShellLayout>();
        services.AddSingleton<ShellNavigator>();
        services.AddSingleton<ITargetAnnouncer, SpectreTargetAnnouncer>();
        services.AddSingleton<IStepRunner, SequentialStepRunner>();
        services.AddSingleton<IRecoveryPrompt, SpectreRecoveryPrompt>();
        services.AddSingleton<ResumePrompt>();

        // La cible de la boîte à outils, exprimée une seule fois dans le vocabulaire du noyau partagé, pour
        // que ni le cœur d'un outil ni sa présentation n'aient à référencer le type d'options de la couche
        // d'infrastructure.
        services.AddSingleton(BuildServerTarget(configuration));
        services.AddSingleton(BuildVarCompareOptions(configuration));

        // Chaque outil s'enregistre par ses propres extensions AddXxx(). Ajouter le second outil revient à
        // ajouter deux lignes ici, sans toucher à aucun outil existant.
        services.AddVarCompareInfrastructure();
        services.AddVarCompare();
        DevToolbox.Tools.VarCompare.Infrastructure.ServiceCollectionExtensions
            .RegisterRedactedTypes(redaction);
    }

    private static async Task<int> RunShellAsync(
        IServiceProvider services,
        IAnsiConsole console,
        AzureDevOpsServerOptions options,
        CancellationToken cancellationToken)
    {
        ShellLayout layout = services.GetRequiredService<ShellLayout>();
        ShellNavigator navigator = services.GetRequiredService<ShellNavigator>();

        IReadOnlyList<ITool> tools = [.. services.GetServices<ITool>()];
        string serverHost = ServerHostOf(options);

        while (!cancellationToken.IsCancellationRequested)
        {
            console.Clear();
            layout.RenderHeader(tools, activeTab: null, serverHost, options.Collection);

            string? tab = navigator.SelectTab(ShellLayout.DistinctTabs(tools));

            if (tab is null)
            {
                return ExitCodes.Success;
            }

            IReadOnlyList<ITool> inTab =
                [.. tools.Where(tool => string.Equals(tool.Tab, tab, StringComparison.Ordinal))];

            ITool? chosen = navigator.SelectTool(inTab);

            if (chosen is not null)
            {
                await chosen.RunAsync(cancellationToken);
            }
        }

        return ExitCodes.Cancelled;
    }

    private static ServerTarget BuildServerTarget(ConfigurationManager configuration)
    {
        AzureDevOpsServerOptions options = new();
        configuration.GetSection(AzureDevOpsServerOptions.SectionName).Bind(options);

        // La même lecture que partout ailleurs : un nom court est une adresse valable, et la cible partagée
        // ne doit pas être la seule à l'ignorer.
        ServerAddress.TryNormalise(options.BaseUrl, options.AllowInsecureHttp, out Uri? address, out _);

        return new ServerTarget(
            address ?? new Uri(options.BaseUrl, UriKind.Absolute), options.Collection, options.ApiVersion);
    }

    private static VarCompareOptions BuildVarCompareOptions(ConfigurationManager configuration)
    {
        AzureDevOpsServerOptions options = new();
        configuration.GetSection(AzureDevOpsServerOptions.SectionName).Bind(options);

        return new VarCompareOptions(options.MaxDegreeOfParallelism);
    }

    private static string ServerHostOf(AzureDevOpsServerOptions options) =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? uri) ? uri.Host : options.BaseUrl;
}
