using DevToolbox.Application.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using System.Net.Http;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Infrastructure.Http;
using DevToolbox.Console;
using DevToolbox.Infrastructure.Logging;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Presentation;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Core.Steps;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Application;

/// <summary>
/// Résout la véritable racine de composition.
/// </summary>
/// <remarks>
/// Un service utilisé mais jamais enregistré n'échoue qu'au moment où le développeur atteint l'écran
/// concerné. Ce test construit le graphe réel issu de <see cref="Program.ConfigureServices"/>, et non une
/// copie, afin qu'un enregistrement manquant fasse échouer la chaîne de construction plutôt que l'après-midi
/// de quelqu'un.
/// </remarks>
public sealed class CompositionRootTests
{
    [Fact]
    public void Every_service_the_toolbox_needs_can_be_resolved()
    {
        using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<ITool>().Should().NotBeNull();
        provider.GetRequiredService<IStepRunner>().Should().NotBeNull();
        provider.GetRequiredService<IVariableGroupGateway>().Should().NotBeNull();
        provider.GetRequiredService<IWorkingFolderInspector>().Should().NotBeNull();
        provider.GetRequiredService<IComparisonPresenter>().Should().NotBeNull();
        provider.GetRequiredService<IRunCheckpointStore>().Should().NotBeNull();
        provider.GetRequiredService<IRunCheckpointFactory>().Should().NotBeNull();
        provider.GetRequiredService<ITargetAnnouncer>().Should().NotBeNull();
        provider.GetRequiredService<IResumedSelectionReconciler>().Should().NotBeNull();
    }

    [Fact]
    public void Pairing_the_operations_resolves_from_the_real_graph()
    {
        // Comme pour la lecture : cet essai ne résout que ce qu'on lui nomme, et une étape absente du
        // fil ne serait jamais construite par la résolution d'ITool. Un enregistrement oublié aurait
        // attendu l'après-midi d'un développeur.
        using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<IWsdlProjector>().Should().NotBeNull();
        provider.GetRequiredService<ICorrespondenceProposer>().Should().NotBeNull();
        provider.GetRequiredService<ICorrespondencePrompt>().Should().NotBeNull();
        provider.GetRequiredService<CorrespondStep>().Should().NotBeNull();
    }

    [Fact]
    public void The_chain_the_real_graph_builds_is_the_chain_the_tool_declares()
    {
        // Résoudre une étape ne prouve pas qu'elle tourne. « ReadWsdl » et « Correspond » étaient
        // écrites, enregistrées, résolues par l'essai ci-dessus et éprouvées une par une — et absentes
        // de la chaîne pendant deux tranches, si bien que l'outil ne savait pas lire un contrat de
        // service alors que tout ce qu'il faut pour le lire existait et passait ses essais.
        using ServiceProvider provider = BuildProvider();

        ApiDeltaTool tool = provider.GetServices<ITool>().OfType<ApiDeltaTool>().Single();

        tool.StepNames.Should().Equal(
            ApiDeltaStepNames.InOrder,
            "l'ordre déclaré et la chaîne construite ne doivent pas pouvoir diverger");
    }

    [Fact]
    public void Reading_a_service_contract_resolves_from_the_real_graph()
    {
        // Ces quatre services n'étaient couverts par rien : cet essai ne résout que ce qu'on lui nomme,
        // et une étape absente de la liste du fil ne serait jamais construite par la résolution d'ITool.
        // Un enregistrement oublié aurait donc attendu l'après-midi d'un développeur, qui est exactement
        // ce que ce fichier existe pour empêcher.
        using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<IWsdlReader>().Should().NotBeNull();
        provider.GetRequiredService<AddressGuard>().Should().NotBeNull();
        provider.GetRequiredService<ReadWsdlStep>().Should().NotBeNull();

        // Le récupérateur est le seul à dépendre d'un client typé : s'il se résout, c'est que la fabrique
        // est en place et que le gestionnaire configuré lui est bien remis.
        provider.GetRequiredService<IContractFetcher>().Should().NotBeNull();
    }

    [Fact]
    public void The_retrieval_budget_is_set_where_it_actually_applies()
    {
        // Le délai avait d'abord été posé sur « HttpClient.Timeout », où le gestionnaire de résilience le
        // remplace par l'infini. Il ne valait 30 secondes que parce que le défaut du gestionnaire vaut
        // 30 secondes : un budget qui n'avait aucun effet et dont rien ne l'aurait dit.
        using ServiceProvider provider = BuildProvider();

        HttpStandardResilienceOptions resilience = provider
            .GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
            .Get(nameof(IContractFetcher) + "-standard");

        resilience.TotalRequestTimeout.Timeout.Should().Be(
            TimeSpan.FromSeconds(WsdlLimits.Default.FetchTimeoutSeconds),
            "c'est là que le budget de récupération décide de quelque chose");

        // Le budget vaut délibérément autre chose que les 30 secondes du gestionnaire : à valeur égale,
        // cet essai aurait passé sans qu'aucune configuration ne soit faite, comme ce fut le cas.
        WsdlLimits.Default.FetchTimeoutSeconds.Should().NotBe(30);

        provider.GetRequiredService<IContractFetcher>().Should().BeOfType<ContractFetcher>();
    }


    [Fact]
    public void The_shell_announcer_wins_over_the_silent_observer_the_http_client_falls_back_to()
    {
        // L'enregistrement du client Azure DevOps pose un observateur muet, mais seulement si personne n'en
        // a posé un. Inverser cet ordre rendrait les reprises de nouveau invisibles, sans rien casser
        // d'autre : c'est exactement le genre de régression qu'aucun autre test ne verrait.
        using ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<IRetryObserver>().Should().BeOfType<SpectreRetryAnnouncer>();
    }

    [Fact]
    public void Each_tool_composes_its_own_named_steps()
    {
        // Les étapes ne sont plus enregistrées sous IToolStep : une collection commune à toute la boîte à
        // outils donnerait à chaque outil les étapes des autres. Chaque outil assemble donc les siennes, et
        // ce test vérifie que chacune est bien enregistrée — une étape écrite mais jamais câblée doit faire
        // échouer la construction, pas disparaître.
        using ServiceProvider provider = BuildProvider();

        provider.GetServices<IToolStep>().Should().BeEmpty(
            "aucune étape ne doit tomber dans la collection commune");

        IReadOnlyList<IToolStep> varCompare =
        [
            provider.GetRequiredService<IdentifyProjectStep>(),
            provider.GetRequiredService<ListGroupsStep>(),
            provider.GetRequiredService<SelectGroupsStep>(),
            provider.GetRequiredService<RetrieveGroupsStep>(),
            provider.GetRequiredService<BuildComparisonStep>(),
        ];

        varCompare.Select(step => step.Name).Should().Equal(
            IdentifyProjectStep.StepName,
            ListGroupsStep.StepName,
            SelectGroupsStep.StepName,
            RetrieveGroupsStep.StepName,
            BuildComparisonStep.StepName);

        provider.GetRequiredService<SelectStep>().Name.Should().Be(ApiDeltaStepNames.Select);
        provider.GetRequiredService<ReadTargetStep>().Name.Should().Be(ApiDeltaStepNames.ReadTarget);
        provider.GetRequiredService<ReadSourceStep>().Name.Should().Be(ApiDeltaStepNames.ReadSource);
        provider.GetRequiredService<ValidateStep>().Name.Should().Be(ApiDeltaStepNames.Validate);
        provider.GetRequiredService<CompareStep>().Name.Should().Be(ApiDeltaStepNames.Compare);
        provider.GetRequiredService<VersionStep>().Name.Should().Be(ApiDeltaStepNames.Version);
        provider.GetRequiredService<AuditStep>().Name.Should().Be(ApiDeltaStepNames.Audit);
        provider.GetRequiredService<PlanStep>().Name.Should().Be(ApiDeltaStepNames.Plan);
        provider.GetRequiredService<WriteStep>().Name.Should().Be(ApiDeltaStepNames.Write);
    }

    [Fact]
    public void Every_step_declares_itself_idempotent()
    {
        // Une étape qu'on ne peut pas rejouer sans risque doit se déclarer non reprenable. Les étapes de
        // varcompare le sont parce qu'elles lisent ; celle d'apidelta parce que redemander ne change rien
        // au monde.
        using ServiceProvider provider = BuildProvider();

        IReadOnlyList<IToolStep> steps =
        [
            provider.GetRequiredService<IdentifyProjectStep>(),
            provider.GetRequiredService<ListGroupsStep>(),
            provider.GetRequiredService<SelectGroupsStep>(),
            provider.GetRequiredService<RetrieveGroupsStep>(),
            provider.GetRequiredService<BuildComparisonStep>(),
            provider.GetRequiredService<SelectStep>(),
            provider.GetRequiredService<ReadTargetStep>(),
            provider.GetRequiredService<ReadSourceStep>(),
            provider.GetRequiredService<ValidateStep>(),
            provider.GetRequiredService<CompareStep>(),
            provider.GetRequiredService<VersionStep>(),
            provider.GetRequiredService<AuditStep>(),
            provider.GetRequiredService<PlanStep>(),
            provider.GetRequiredService<WriteStep>(),
        ];

        steps.Should().OnlyContain(step => step.IsIdempotent);
    }

    [Fact]
    public void Both_tools_appear_each_under_its_own_tab()
    {
        using ServiceProvider provider = BuildProvider();

        IReadOnlyList<ITool> tools = [.. provider.GetServices<ITool>()];

        ITool varCompare = tools.Single(tool => string.Equals(tool.Id, "varcompare", StringComparison.Ordinal));
        ITool apiDelta = tools.Single(tool => string.Equals(tool.Id, "apidelta", StringComparison.Ordinal));

        varCompare.Tab.Should().Be("Azure DevOps");
        varCompare.Description.Should().Contain("lecture seule");
        varCompare.RequiresServer.Should().BeTrue();

        apiDelta.Tab.Should().Be("OpenAPI");
        apiDelta.RequiresServer.Should().BeFalse(
            "apidelta lit deux fichiers locaux et n'émet aucune requête");
    }

    [Fact]
    public void Each_tool_has_the_checkpoint_factory_that_knows_its_runs()
    {
        // Le moteur d'étapes est partagé, les fabriques ne le sont pas : il choisit par l'identifiant de
        // l'outil. Sans cela, la dernière enregistrée l'emporterait et la reprise du second outil
        // cesserait de fonctionner sans que rien ne le dise.
        using ServiceProvider provider = BuildProvider();

        provider.GetServices<IRunCheckpointFactory>().Select(factory => factory.ToolId)
            .Should().BeEquivalentTo("varcompare", "apidelta");
    }

    [Fact]
    public void Value_bearing_types_are_registered_with_the_sink_level_redactor()
    {
        // L'enregistrement lui-même est le garde-fou : c'est lui qui rend une ligne de journalisation erronée
        // inoffensive au lieu d'être une fuite.
        RedactionRegistry redaction = new();
        _ = BuildProvider(redaction);

        redaction.IsForbidden(typeof(VariableEntry)).Should().BeTrue();
        redaction.IsForbidden(typeof(VariableGroupSnapshot)).Should().BeTrue();
        redaction.IsForbidden(typeof(DevToolbox.Tools.ApiDelta.Core.Syntax.SpecNode)).Should().BeTrue();
        redaction.IsForbidden(typeof(DevToolbox.Tools.ApiDelta.Core.Syntax.MappingEntry))
            .Should().BeTrue();
        redaction.IsForbidden(typeof(DevToolbox.Tools.ApiDelta.Core.Syntax.ResolvedScalar))
            .Should().BeTrue();
        redaction.IsForbidden(typeof(DevToolbox.Tools.ApiDelta.Core.Comparison.ValueSummary))
            .Should().BeTrue("un résumé de valeur n'a rien à faire dans un journal");
        redaction.IsForbidden(typeof(string)).Should().BeFalse();
    }

    private static ServiceProvider BuildProvider(RedactionRegistry? redaction = null)
    {
        ConfigurationManager configuration = new();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AzureDevOpsServer:BaseUrl"] = "https://devops.entreprise.local",
            ["AzureDevOpsServer:Collection"] = "DefaultCollection",
            ["AzureDevOpsServer:ApiVersion"] = "7.1",
            ["AzureDevOpsServer:MaxDegreeOfParallelism"] = "5",
        });

        ServiceCollection services = [];
        services.AddLogging(builder => builder.ClearProviders());

        Program.ConfigureServices(
            services,
            configuration,
            new AppPaths(),
            redaction ?? new RedactionRegistry(),
            new TestConsole());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
