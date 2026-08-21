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
        services.AddSingleton<ComparisonSession>();

        services.AddSingleton<IRunCheckpointFactory, VarCompareCheckpointFactory>();

        // Les cinq étapes nommées, dans l'ordre d'exécution.
        services.AddSingleton<IToolStep, IdentifyProjectStep>();
        services.AddSingleton<IToolStep, ListGroupsStep>();
        services.AddSingleton<IToolStep, SelectGroupsStep>();
        services.AddSingleton<IToolStep>(CreateRetrieveGroupsStep);
        services.AddSingleton<IToolStep, BuildComparisonStep>();

        services.AddSingleton<ITool, VarCompareTool>();

        return services;
    }

    private static RetrieveGroupsStep CreateRetrieveGroupsStep(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IVariableGroupGateway>(),
            provider.GetRequiredService<IRetrievalProgress>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<VarCompareOptions>().MaxDegreeOfParallelism);
}
