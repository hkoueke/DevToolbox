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
/// <para>
/// Les choix distinguent la reprise, l'oubli immédiat et la conservation jusqu'au prochain enregistrement.
/// </para>
/// </remarks>
public sealed class ResumePrompt
{
    private const string ResumeLabel = "Reprendre les choix enregistrés";
    private const string DiscardLabel = "Nouvelle session — oublier les anciens choix";
    private const string StartFreshLabel = "Nouvelle session — garder les anciens choix provisoirement";

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
    /// La réponse du développeur. <em>Oublier</em> supprime la séance mémorisée ; <em>me la redemander</em>
    /// la laisse en place jusqu'au prochain enregistrement de cet outil. Un seul point de
    /// reprise existe par outil : une séance gardée est remplacée dès que les nouveaux choix sont enregistrés, et le
    /// libellé explique donc cette conservation provisoire.
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
            .Header("[bold]Des choix précédents sont disponibles[/]")
            .Border(BoxBorder.Rounded));

        _console.MarkupLine("[grey]Reprendre réutilise les choix ci-dessus et relit les données. Aucun ancien résultat n’est réutilisé.[/]");
        _console.MarkupLine("[grey]Nouvelle session redemande les choix. Aucun de ces choix ne supprime vos fichiers ni vos rapports.[/]");
        _console.MarkupLine("[grey]Si vous gardez les anciens choix, ils restent disponibles jusqu’au premier enregistrement automatique de la nouvelle session.[/]");

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

    /// <summary>Explique un oubli impossible sans présenter une remise à zéro comme réussie.</summary>
    public static void RenderCleanupWarning(IAnsiConsole console, DevToolbox.Domain.Runs.ToolRunContext context)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Get<bool>(CheckpointContextKeys.CleanupFailed))
        {
            if (context.Run.Outcome == DevToolbox.Domain.Runs.RunOutcome.Abandoned)
            {
                console.MarkupLine("[yellow]La remise à zéro n’a pas eu lieu. Le traitement a été arrêté avec les anciens choix.[/]");
            }

            console.MarkupLine("[yellow]La session enregistrée n’a pas pu être oubliée. Les anciens choix peuvent encore être proposés à la prochaine ouverture. Vos fichiers et rapports sont conservés.[/]");
        }
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
