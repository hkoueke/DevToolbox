using DevToolbox.Application.Abstractions;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Propose au démarrage suivant de reprendre ou d'oublier une exécution interrompue.
/// </summary>
/// <remarks>
/// Ce qui est proposé est une <em>sélection</em>, jamais un résultat : seuls les choix du développeur sont
/// mémorisés, et reprendre les relit au lieu d'afficher un contenu mémorisé. L'invite le dit
/// explicitement, car un développeur qui agirait sur une image périmée est précisément ce que ce mécanisme
/// évite.
/// <para>
/// L'invite ne connaît aucun outil : elle affiche les éléments que l'outil a choisi de conserver, dans
/// l'ordre où il les a écrits. C'est ce qui lui permet de servir des sélections aussi différentes qu'une
/// liste de groupes de variables et deux fichiers accompagnés d'un mode.
/// </para>
/// </remarks>
public sealed class ResumePrompt
{
    private const string ResumeLabel = "Reprendre (la sélection sera relue)";
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
            "[grey]Seule votre sélection a été mémorisée. Reprendre la relit, si bien que vous n'agissez "
            + "jamais sur une image périmée.[/]");

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

        grid.AddRow("[grey]outil[/]", Markup.Escape(run.ToolId));

        // Les éléments sont regroupés par sorte, dans l'ordre d'écriture : l'outil décide de ce qui mérite
        // d'être montré, et l'invite n'a pas à savoir ce que les mots veulent dire.
        foreach (IGrouping<string, PersistedSelectionEntry> kind in run.Selection.GroupBy(
            entry => entry.Kind, StringComparer.Ordinal))
        {
            grid.AddRow(
                "[grey]" + Markup.Escape(kind.Key) + "[/]",
                Markup.Escape(string.Join(", ", kind.Select(entry => entry.Label))));
        }

        grid.AddRow("[grey]démarrée le[/]", run.StartedAt.ToLocalTime().ToString("u", null));
        grid.AddRow("[grey]parvenue à[/]", DescribeProgress(run));

        return grid;
    }

    private static string DescribeProgress(PersistedRun run) =>
        run.CompletedSteps.Count == 0
            ? "[grey]aucune étape n'a été achevée[/]"
            : Markup.Escape(run.CompletedSteps[^1]);
}
