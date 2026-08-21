using DevToolbox.Application.Abstractions;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Propose au démarrage suivant de reprendre ou d'oublier une exécution interrompue.
/// </summary>
/// <remarks>
/// Ce qui est proposé est une <em>sélection</em>, jamais un résultat : la collection, le projet et les
/// groupes choisis sont mémorisés, et reprendre relit ces groupes au lieu d'afficher un contenu mémorisé.
/// L'invite le dit explicitement, car un développeur qui agirait sur une image périmée est précisément ce
/// que ce mécanisme évite.
/// </remarks>
public sealed class ResumePrompt
{
    private const string ResumeLabel = "Resume it (the groups will be read again)";
    private const string DiscardLabel = "Discard it and start fresh";
    private const string StartFreshLabel = "Start fresh";

    private readonly IAnsiConsole _console;

    /// <summary>Crée l'invite.</summary>
    /// <param name="console">La console où présenter l'invite.</param>
    public ResumePrompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Demande quoi faire de l'exécution interrompue la plus récente.</summary>
    /// <param name="candidates">Les exécutions reprenables, la plus récente en tête.</param>
    /// <returns>
    /// La réponse du développeur. Noter qu'<em>oublier</em> et <em>repartir de zéro</em> diffèrent : oublier
    /// supprime l'exécution mémorisée, tandis que repartir de zéro la laisse disponible pour un autre jour.
    /// </returns>
    public ResumeAnswer Ask(IReadOnlyList<PersistedRun> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return new ResumeAnswer(ResumeDecision.StartFresh, null);
        }

        PersistedRun candidate = candidates[0];

        _console.Write(new Panel(Describe(candidate))
            .Header("[bold]An earlier run did not finish[/]")
            .Border(BoxBorder.Rounded));

        _console.MarkupLine(
            "[grey]Only your selection was remembered. Resuming reads the groups again, so you never act "
            + "on a stale picture.[/]");

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices(ResumeLabel, DiscardLabel, StartFreshLabel));

        ResumeDecision decision = chosen switch
        {
            ResumeLabel => ResumeDecision.Resume,
            DiscardLabel => ResumeDecision.Discard,
            _ => ResumeDecision.StartFresh,
        };

        return new ResumeAnswer(decision, candidate);
    }

    private static Grid Describe(PersistedRun run)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow("[grey]collection[/]", Markup.Escape(run.Collection));
        grid.AddRow("[grey]project[/]", Markup.Escape(run.Project));
        grid.AddRow(
            "[grey]groups[/]",
            Markup.Escape(string.Join(", ", run.SelectedGroups.Select(group => group.Name))));
        grid.AddRow("[grey]started[/]", run.StartedAt.ToLocalTime().ToString("u", null));
        grid.AddRow("[grey]got as far as[/]", DescribeProgress(run));

        return grid;
    }

    private static string DescribeProgress(PersistedRun run) =>
        run.CompletedSteps.Count == 0
            ? "[grey]nothing was completed[/]"
            : Markup.Escape(run.CompletedSteps[^1]);
}
