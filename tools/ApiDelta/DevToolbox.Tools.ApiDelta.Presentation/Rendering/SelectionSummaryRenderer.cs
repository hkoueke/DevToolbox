using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Rendering;

/// <summary>
/// Dit ce qui a été retenu, et ce que l'exécution va — ou ne va pas — en faire.
/// </summary>
/// <remarks>
/// Les étapes sautées sont nommées avec leur raison. Une étape passée en silence laisserait croire qu'elle
/// a été accomplie, et c'est précisément la confusion que la liste d'étapes existe pour éviter.
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

        grid.AddRow("[grey]ce qui est fait[/]", Markup.Escape(RunModeNames.Of(selection.Mode)));
        grid.AddRow("[grey]fichier en service[/]", Describe(selection.ProductionPath, selection.ProductionHash));

        if (selection.CandidatePath is { } candidate)
        {
            grid.AddRow("[grey]nouveau fichier[/]", Describe(candidate, selection.CandidateHash));
        }

        grid.AddRow("[grey]rapports écrits dans[/]", Markup.Escape(selection.OutputDirectory));

        _console.Write(new Panel(grid)
            .Header("[bold]apidelta[/]")
            .Border(BoxBorder.Rounded));

        RenderSteps(run, selection.Mode);

        // La production n'est pas touchée, et le dire explicitement vaut mieux que de le laisser déduire.
        _console.MarkupLine(
            "[grey]Aucun de vos fichiers n'est modifié : l'outil ne fait que lire et écrire des rapports.[/]");
    }

    private void RenderSteps(ToolRun run, RunMode mode)
    {
        foreach (StepState step in run.Steps)
        {
            _console.MarkupLine(step.Status switch
            {
                StepStatus.Completed => "[green]✓[/] " + Markup.Escape(step.Name),
                StepStatus.Skipped => "[grey]–[/] [grey]" + Markup.Escape(step.Name) + " · "
                    + Markup.Escape(ApiDeltaStepNames.SkipReason(mode)) + "[/]",
                StepStatus.Failed => "[red]×[/] " + Markup.Escape(step.Name)
                    + " · " + Markup.Escape(step.FailureReason ?? string.Empty),
                _ => "[grey]·[/] [grey]" + Markup.Escape(step.Name) + "[/]",
            });
        }
    }

    private static string Describe(string path, string? hash) =>
        string.IsNullOrWhiteSpace(hash)
            ? Markup.Escape(path)
            : Markup.Escape(path) + " [grey]· sha256:" + Markup.Escape(hash[..Math.Min(12, hash.Length)])
                + "[/]";
}
