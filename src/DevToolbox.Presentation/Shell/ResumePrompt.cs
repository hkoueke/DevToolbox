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
    private const string ResumeLabel = "Reprendre (les groupes seront relus)";
    private const string DiscardLabel = "Oublier cette exécution et repartir de zéro";
    private const string StartFreshLabel = "Repartir de zéro";

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
            .Header("[bold]Une exécution précédente ne s'est pas terminée[/]")
            .Border(BoxBorder.Rounded));

        _console.MarkupLine(
            "[grey]Seule votre sélection a été mémorisée. Reprendre relit les groupes, si bien que vous "
            + "n'agissez jamais sur une image périmée.[/]");

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Que souhaitez-vous faire ?")
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
        grid.AddRow("[grey]projet[/]", Markup.Escape(run.Project));
        grid.AddRow(
            "[grey]groupes[/]",
            Markup.Escape(string.Join(", ", run.SelectedGroups.Select(group => group.Name))));
        grid.AddRow("[grey]démarrée le[/]", run.StartedAt.ToLocalTime().ToString("u", null));
        grid.AddRow("[grey]parvenue à[/]", DescribeProgress(run));

        return grid;
    }

    private static string DescribeProgress(PersistedRun run) =>
        run.CompletedSteps.Count == 0
            ? "[grey]aucune étape n'a été achevée[/]"
            : Markup.Escape(run.CompletedSteps[^1]);
}
