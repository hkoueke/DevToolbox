using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Rendering;

/// <summary>
/// Dit ce qui a été retenu, et ce que l'exécution va — ou ne va pas — en faire.
/// </summary>
/// <remarks>
/// Le bilan explique les étapes non demandées selon le mode choisi. Les erreurs restent détaillées ;
/// les fichiers produits ne sont annoncés qu’à partir des écritures réellement terminées.
/// </remarks>
public sealed class SelectionSummaryRenderer
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée le moteur de rendu.</summary>
    /// <param name="console">La console où écrire.</param>
    public SelectionSummaryRenderer(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Affiche la sélection retenue et l'état des étapes.</summary>
    /// <param name="selection">Ce qui a été choisi.</param>
    /// <param name="run">L'exécution, pour l'état de ses étapes.</param>
    public void Render(RunSelection selection, ToolRun run)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(run);

        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow("[grey]objectif[/]", Markup.Escape(RunModeNames.Of(selection.Mode)));
        grid.AddRow("[grey]fichier de référence[/]", Markup.Escape(selection.ProductionPath));

        if (selection.CandidatePath is { } candidate)
        {
            grid.AddRow("[grey]fichier à comparer[/]", Markup.Escape(candidate));
        }

        grid.AddRow("[grey]dossier des résultats[/]", Markup.Escape(selection.OutputDirectory));

        _console.Write(new Panel(grid)
            .Header("[bold]apidelta[/]")
            .Border(BoxBorder.Rounded));

        RenderSteps(run, selection.Mode);

    }

    /// <summary>Affiche les fichiers réellement écrits après une exécution réussie.</summary>
    public void RenderWritten(RunSelection selection, IReadOnlyList<Artifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(artifacts);
        bool optimized = artifacts.Any(artifact => artifact.Kind == ArtifactKind.OptimizedDocument);
        _console.MarkupLine(optimized && selection.InPlace
            ? "[yellow]Votre fichier d’origine a été remplacé par la version nettoyée.[/]"
            : "[green]Votre fichier d’origine est conservé.[/]");
        if (selection.NeedsCorrection && !artifacts.Any(artifact => artifact.Kind == ArtifactKind.ConsolidatedDocument))
        {
            _console.MarkupLine("[grey]Aucun fichier fusionné n’a été créé.[/]");
        }

        if (selection.ApplySafeOptimizations && !optimized)
        {
            _console.MarkupLine("[grey]Aucun fichier nettoyé n’a été créé. Consultez le bilan du nettoyage.[/]");
        }

        foreach (Artifact artifact in artifacts.OrderBy(artifact => artifact.Kind == ArtifactKind.DeltaJson ? 1 : 0))
        {
            _console.MarkupLine("[grey]" + ArtifactLabel(artifact.Kind) + " :[/] " + Markup.Escape(artifact.Path));
        }
    }

    private static string ArtifactLabel(ArtifactKind kind) => kind switch
    {
        ArtifactKind.ConsolidatedDocument => "Fichier fusionné",
        ArtifactKind.OptimizedDocument => "Fichier nettoyé",
        ArtifactKind.AuditReport => "Rapport de vérification",
        ArtifactKind.OptimizationPlan => "Propositions de nettoyage",
        ArtifactKind.DeltaReport => "Rapport de comparaison",
        ArtifactKind.Manifest => "Détail des modifications appliquées",
        _ => "Résultat pour les outils automatiques",
    };

    private void RenderSteps(ToolRun run, RunMode mode)
    {
        if (run.Outcome == RunOutcome.Success)
        {
            _console.MarkupLine("[green]Traitement terminé.[/]");
        }

        if (run.Steps.Any(step => step.Status == StepStatus.Skipped))
        {
            _console.MarkupLine(mode == RunMode.Analyse
                ? "[grey]Ce parcours vérifie un seul fichier ; il ne compare pas deux versions.[/]"
                : "[grey]Ce parcours compare deux fichiers. Le nettoyage se demande depuis l’autre choix du menu.[/]");
        }

        foreach (StepState step in run.Steps.Where(step => step.Status is not (StepStatus.Completed or StepStatus.Skipped)))
        {
            _console.MarkupLine(step.Status switch
            {
                StepStatus.Failed => "[red]×[/] " + Markup.Escape(step.Name)
                    + " · " + Markup.Escape(step.FailureReason ?? string.Empty),
                _ => "[grey]·[/] [grey]" + Markup.Escape(step.Name) + "[/]",
            });
        }
    }

}
