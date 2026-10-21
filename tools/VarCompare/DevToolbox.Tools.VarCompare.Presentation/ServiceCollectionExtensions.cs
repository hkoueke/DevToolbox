using DevToolbox.Application.Abstractions;
using DevToolbox.Tools.VarCompare.Core;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Steps;
using DevToolbox.Tools.VarCompare.Presentation.Prompts;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolbox.Tools.VarCompare.Presentation;

/// <summary>
/// Enregistre les parties de varcompare situées au-dessus de l'infrastructure : invites, rendu, les cinq
/// étapes et l'outil lui-même.
/// </summary>
/// <remarks>
/// La passerelle et l'inspecteur de dossier sont enregistrés à part, par le projet d'infrastructure de
/// l'outil, car cet assemblage ne peut volontairement pas référencer l'infrastructure. La racine de
/// composition appelle les deux.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>Enregistre les étapes, les invites, le rendu et l'outil varcompare.</summary>
    /// <param name="services">La collection de services.</param>
    /// <returns>La collection de services, pour le chaînage.</returns>
    public static IServiceCollection AddVarCompare(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProjectChooser, ProjectPrompt>();
        services.AddSingleton<IGroupChooser, GroupSelectionPrompt>();
        services.AddSingleton<IRetrievalProgress, RetrievalProgress>();
        services.AddSingleton<ComparisonPresenter>();
        services.AddSingleton<IComparisonPresenter>(p => p.GetRequiredService<ComparisonPresenter>());
        services.AddSingleton(p => new SymbolSet(
            p.GetRequiredService<DevToolbox.Presentation.Shell.ConsoleCapabilities>().SupportsUnicode));
        services.AddSingleton<VariableDetailRenderer>();
        services.AddSingleton<FailureRenderer>();
        services.AddSingleton<AbortSummaryRenderer>();
        services.AddSingleton<ResumeCoordinator>();
        services.AddSingleton<IResumedSelectionReconciler>(p => p.GetRequiredService<ResumeCoordinator>());
        services.AddSingleton<ComparisonSession>();

        services.AddSingleton<IRunCheckpointFactory, VarCompareCheckpointFactory>();

        // Les cinq étapes nommées, enregistrées par leur type concret et assemblées dans l'ordre pour cet
        // outil seul. Les enregistrer sous IToolStep les mettrait dans une collection commune à toute la
        // boîte à outils, où le second outil recevrait les étapes du premier.
        services.AddSingleton<IdentifyProjectStep>();
        services.AddSingleton<ListGroupsStep>();
        services.AddSingleton<SelectGroupsStep>();
        services.AddSingleton(CreateRetrieveGroupsStep);
        services.AddSingleton<BuildComparisonStep>();

        services.AddSingleton<ITool>(CreateTool);

        return services;
    }

    /// <summary>Assemble l'outil avec ses cinq étapes, dans l'ordre d'exécution.</summary>
    private static VarCompareTool CreateTool(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IStepRunner>(),
            [
                provider.GetRequiredService<IdentifyProjectStep>(),
                provider.GetRequiredService<ListGroupsStep>(),
                provider.GetRequiredService<SelectGroupsStep>(),
                provider.GetRequiredService<RetrieveGroupsStep>(),
                provider.GetRequiredService<BuildComparisonStep>(),
            ],
            provider.GetRequiredService<ComparisonSession>(),
            provider.GetRequiredService<ITargetAnnouncer>(),
            provider.GetRequiredService<DevToolbox.Domain.AzureDevOps.ServerTarget>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ResumeCoordinator>(),
            provider.GetRequiredService<Rendering.AbortSummaryRenderer>(),
            provider.GetRequiredService<Spectre.Console.IAnsiConsole>());

    private static RetrieveGroupsStep CreateRetrieveGroupsStep(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IVariableGroupGateway>(),
            provider.GetRequiredService<IRetrievalProgress>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<VarCompareOptions>().MaxDegreeOfParallelism);
}
