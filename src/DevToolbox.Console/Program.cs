using DevToolbox.Application.Abstractions;
using DevToolbox.Application.Runs;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Infrastructure;
using DevToolbox.Infrastructure.AzureDevOps;
using DevToolbox.Infrastructure.Logging;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Infrastructure;
using DevToolbox.Tools.ApiDelta.Presentation;
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
            return await RunToolboxAsync(args, console, cancellation.Token);
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

    /// <summary>
    /// Construit l'hôte, lance la coquille, et recommence si l'adresse du serveur vient d'être confirmée.
    /// </summary>
    /// <remarks>
    /// La boucle ne tourne une seconde fois que dans un cas : un outil adressé au serveur a été choisi
    /// alors qu'aucune adresse n'était enregistrée. L'adresse vient d'être confirmée, et les options d'un
    /// hôte construit étant déjà liées, c'est un hôte neuf qui la portera.
    /// </remarks>
    private static async Task<int> RunToolboxAsync(
        string[] args,
        IAnsiConsole console,
        CancellationToken cancellationToken)
    {
        AppPaths paths = new();
        paths.EnsureCreated();

        ServerConfigurationResolver resolver = new(paths, console);
        string? toolToRun = null;

        while (true)
        {
            IReadOnlyDictionary<string, string?>? stored =
                await resolver.TryLoadStoredOverridesAsync(cancellationToken);

            using IHost host = BuildHost(args, console, paths, stored);

            ShellResult result = await RunAsync(
                host, console, resolver, stored is not null, toolToRun, cancellationToken);

            if (result.ToolToRunAfterRestart is null)
            {
                return result.ExitCode;
            }

            toolToRun = result.ToolToRunAfterRestart;
        }
    }

    private static IHost BuildHost(
        string[] args,
        IAnsiConsole console,
        AppPaths paths,
        IReadOnlyDictionary<string, string?>? serverOverrides)
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

        // L'adresse du serveur provient de ce que le développeur a confirmé, et non du fichier livré. Elle
        // est superposée avant que quoi que ce soit ne s'y lie — mais elle n'est pas demandée ici : tant
        // qu'aucun outil n'en a besoin, il n'y a pas de question à poser, et un outil hors ligne doit
        // pouvoir servir sur un poste qui n'a jamais vu de serveur.
        if (serverOverrides is not null)
        {
            builder.Configuration.AddInMemoryCollection(serverOverrides);
        }

        RedactionRegistry redaction = new();

        ConfigureLogging(builder.Logging, builder.Configuration, paths, redaction);
        ConfigureServices(builder.Services, builder.Configuration, paths, redaction, console);

        return builder.Build();
    }

    private static async Task<ShellResult> RunAsync(
        IHost host,
        IAnsiConsole console,
        ServerConfigurationResolver resolver,
        bool serverConfigured,
        string? toolToRun,
        CancellationToken cancellationToken)
    {
        try
        {
            // Les exécutions interrompues périmées sont nettoyées au démarrage, avant toute proposition.
            IRunCheckpointStore checkpoints = host.Services.GetRequiredService<IRunCheckpointStore>();
            await checkpoints.SweepExpiredAsync(RunRetention, cancellationToken);

            return await RunShellAsync(
                host, console, resolver, serverConfigured, toolToRun, cancellationToken);
        }
        catch (OptionsValidationException exception)
        {
            return new ShellResult(ReportInvalidConfiguration(console, exception), null);
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

        // Enregistré AVANT le client Azure DevOps : celui-ci ne pose un observateur muet que si personne
        // n'en a déjà posé un.
        services.AddSingleton<IRetryObserver, SpectreRetryAnnouncer>();

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

        // Chaque outil s'enregistre par ses propres extensions AddXxx().
        services.AddVarCompareInfrastructure();
        services.AddVarCompare();
        DevToolbox.Tools.VarCompare.Infrastructure.ServiceCollectionExtensions
            .RegisterRedactedTypes(redaction);

        services.AddSingleton(ApiDeltaOptions.Default);
        services.AddApiDeltaInfrastructure();
        services.AddApiDelta();
        DevToolbox.Tools.ApiDelta.Infrastructure.ServiceCollectionExtensions
            .RegisterRedactedTypes(redaction);
    }

    private static async Task<ShellResult> RunShellAsync(
        IHost host,
        IAnsiConsole console,
        ServerConfigurationResolver resolver,
        bool serverConfigured,
        string? toolToRun,
        CancellationToken cancellationToken)
    {
        IServiceProvider services = host.Services;
        ShellLayout layout = services.GetRequiredService<ShellLayout>();
        ShellNavigator navigator = services.GetRequiredService<ShellNavigator>();

        IReadOnlyList<ITool> tools = [.. services.GetServices<ITool>()];

        // Au retour d'une reconstruction : l'outil choisi juste avant que l'adresse ne soit demandée.
        if (toolToRun is not null)
        {
            ITool? pending = tools.FirstOrDefault(
                tool => string.Equals(tool.Id, toolToRun, StringComparison.Ordinal));

            if (pending is not null)
            {
                await pending.RunAsync(cancellationToken);
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            console.Clear();
            layout.RenderHeader(tools, activeTab: null, ServerLabel(services, serverConfigured));

            string? tab = navigator.SelectTab(ShellLayout.DistinctTabs(tools));

            if (tab is null)
            {
                return new ShellResult(ExitCodes.Success, null);
            }

            IReadOnlyList<ITool> inTab =
                [.. tools.Where(tool => string.Equals(tool.Tab, tab, StringComparison.Ordinal))];

            ITool? chosen = navigator.SelectTool(inTab);

            if (chosen is null)
            {
                continue;
            }

            if (chosen.RequiresServer && !serverConfigured)
            {
                await resolver.PromptAndStoreAsync(
                    services.GetRequiredService<IConfiguration>(), cancellationToken);

                return new ShellResult(ExitCodes.Success, chosen.Id);
            }

            await chosen.RunAsync(cancellationToken);
        }

        return new ShellResult(ExitCodes.Cancelled, null);
    }

    /// <summary>La cible à afficher en en-tête, ou <see langword="null"/> quand il n'y en a pas.</summary>
    private static string? ServerLabel(IServiceProvider services, bool serverConfigured)
    {
        if (!serverConfigured)
        {
            return null;
        }

        AzureDevOpsServerOptions options = services
            .GetRequiredService<IOptions<AzureDevOpsServerOptions>>().Value;

        return ServerHostOf(options) + " ▸ " + options.Collection;
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
