using DevToolbox.Application.Abstractions;
using DevToolbox.Console;
using DevToolbox.Infrastructure.Logging;
using DevToolbox.Infrastructure.Platform;
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
    }

    [Fact]
    public void The_five_named_steps_are_registered_in_execution_order()
    {
        // L'exécution est une suite ordonnée d'étapes nommées, dont le développeur voit l'avancement.
        using ServiceProvider provider = BuildProvider();

        IReadOnlyList<IToolStep> steps = [.. provider.GetServices<IToolStep>()];

        steps.Select(step => step.Name).Should().Equal(
            IdentifyProjectStep.StepName,
            ListGroupsStep.StepName,
            SelectGroupsStep.StepName,
            RetrieveGroupsStep.StepName,
            BuildComparisonStep.StepName);
    }

    [Fact]
    public void Every_step_declares_itself_idempotent_because_they_are_all_reads()
    {
        // Une étape qu'on ne peut pas rejouer sans risque doit se déclarer non reprenable. C'est le fait
        // d'être en lecture seule qui rend ici la poursuite toujours sûre.
        using ServiceProvider provider = BuildProvider();

        provider.GetServices<IToolStep>().Should().OnlyContain(step => step.IsIdempotent);
    }

    [Fact]
    public void The_tool_appears_under_a_tab_with_a_read_only_description()
    {
        using ServiceProvider provider = BuildProvider();

        ITool tool = provider.GetRequiredService<ITool>();

        tool.Id.Should().Be("varcompare");
        tool.Tab.Should().Be("Azure DevOps");
        tool.Description.Should().Contain("read-only");
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
