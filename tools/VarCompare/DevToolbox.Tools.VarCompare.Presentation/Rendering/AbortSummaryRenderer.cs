using DevToolbox.Domain.Runs;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Core.Steps;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Indique clairement quels groupes ont été lus et lesquels ne l'ont pas été lorsqu'une exécution s'arrête
/// avant terme.
/// </summary>
/// <remarks>
/// Laisser les choses dans un état décrit est tout l'enjeu. Un développeur qui abandonne à mi-parcours doit
/// savoir exactement jusqu'où l'outil est allé, faute de quoi il ne peut pas juger si ce qu'il a vu était
/// complet.
/// </remarks>
public sealed class AbortSummaryRenderer
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée le moteur de rendu.</summary>
    /// <param name="console">La console où écrire.</param>
    public AbortSummaryRenderer(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Décrit jusqu'où l'exécution est allée.</summary>
    /// <param name="run">L'exécution arrêtée avant terme.</param>
    /// <param name="context">Le contexte, pour la sélection et ce qui a été obtenu.</param>
    public void Render(ToolRun run, ToolRunContext context)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<VariableGroupSummary> selected =
            context.Get<IReadOnlyList<VariableGroupSummary>>(VarCompareContextKeys.SelectedGroups) ?? [];

        Dictionary<int, VariableGroupSnapshot> snapshots =
            context.Get<Dictionary<int, VariableGroupSnapshot>>(VarCompareContextKeys.Snapshots) ?? [];

        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow("[grey]outcome[/]", DescribeOutcome(run.Outcome));

        if (selected.Count == 0)
        {
            grid.AddRow("[grey]groups[/]", "[grey]no groups had been chosen yet[/]");
        }
        else
        {
            grid.AddRow("[grey]read[/]", Describe(selected.Where(g => snapshots.ContainsKey(g.Id))));
            grid.AddRow("[grey]not read[/]", Describe(selected.Where(g => !snapshots.ContainsKey(g.Id))));
        }

        grid.AddRow("[grey]changed[/]", "[green]nothing — this tool only ever reads[/]");

        _console.Write(new Panel(grid)
            .Header("[yellow]The run ended early[/]")
            .Border(BoxBorder.Rounded));
    }

    private static string Describe(IEnumerable<VariableGroupSummary> groups)
    {
        string[] names = [.. groups.Select(group => group.Name)];

        return names.Length == 0
            ? "[grey]none[/]"
            : Markup.Escape(string.Join(", ", names));
    }

    private static string DescribeOutcome(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Abandoned => "you chose to abort",
        RunOutcome.Cancelled => "cancelled",
        RunOutcome.Failed => "a step failed and was not recovered",
        _ => outcome.ToString(),
    };
}
