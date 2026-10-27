using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Versioning;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Rendering;

/// <summary>
/// Montre d'abord le compte, ensuite le détail que le développeur demande.
/// </summary>
/// <remarks>
/// Un terminal ne porte pas quatre mille différences : une table de cette taille n'est pas de
/// l'information. Le résumé répond à la question qu'on se pose en arrivant — est-ce que je casse quelque
/// chose, et combien — et les artefacts portent le reste. Le détail se demande, écran par écran.
/// </remarks>
public sealed class ComparisonView
{
    private const int PageSize = 15;

    private const string BreakingOnly = "Voir seulement ce qui casse les appels existants";

    private const string Everything = "Voir toutes les différences, une par une";

    private readonly IAnsiConsole _console;

    /// <summary>Crée la vue.</summary>
    /// <param name="console">La console où écrire.</param>
    public ComparisonView(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Affiche le résumé, puis propose d'entrer dans le détail.</summary>
    /// <param name="differences">Ce qui a changé.</param>
    /// <param name="artifacts">Les artefacts écrits.</param>
    /// <param name="version">La version recommandée, quand il y en a une.</param>
    /// <param name="cancellationToken">Annule la navigation.</param>
    public void Show(
        IReadOnlyList<Difference> differences,
        IReadOnlyList<Artifact> artifacts,
        VersionRecommendation? version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(differences);
        ArgumentNullException.ThrowIfNull(artifacts);

        RenderVersion(version);
        RenderSummary(differences, artifacts);

        while (!cancellationToken.IsCancellationRequested)
        {
            string chosen = _console.Prompt(
                new SelectionPrompt<string>()
                    .Title("Que souhaitez-vous regarder ?")
                    .AddChoices(
                        BreakingOnly,
                        Everything,
                        NavigationLabels.Back));

            if (string.Equals(chosen, NavigationLabels.Back, StringComparison.Ordinal))
            {
                return;
            }

            IReadOnlyList<Difference> selected = string.Equals(
                chosen, BreakingOnly, StringComparison.Ordinal)
                ? [.. differences.Where(difference => difference.Impact == ChangeImpact.Breaking)]
                : differences;

            RenderPaged(selected, chosen, cancellationToken);
        }
    }

    /// <summary>
    /// Annonce le numéro à publier avant le détail : c'est la question qu'on se pose en arrivant.
    /// </summary>
    private void RenderVersion(VersionRecommendation? version)
    {
        if (version is null)
        {
            return;
        }

        if (version.Recommended is { } recommended && version.Baseline is { } baseline)
        {
            _console.MarkupLine(
                "[bold]Version recommandée[/] " + Markup.Escape(baseline.ToString()) + " → "
                + Markup.Escape(recommended.ToString()) + " [grey](" + Label(version.Increment) + ")[/]");
        }
        else if (version.BaselineProblem is { } problem)
        {
            _console.MarkupLine("[yellow]" + Markup.Escape(problem) + "[/]");
        }

        if (version.RequiresReview)
        {
            // Le dire ici plutôt que dans le seul rapport : une recommandation qui dépend de changements
            // non classés ne doit pas se lire comme une recommandation sûre.
            _console.MarkupLine(
                "[yellow]" + version.Unreviewed.Count.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                + " changement(s) n'ont pas pu être classés : la recommandation ne vaut qu'autant "
                + "qu'ils auront été relus.[/]");
        }

        _console.WriteLine();
    }

    private void RenderSummary(IReadOnlyList<Difference> differences, IReadOnlyList<Artifact> artifacts)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        foreach (ChangeImpact impact in new[]
        {
            ChangeImpact.Breaking, ChangeImpact.NonBreaking,
            ChangeImpact.DocumentationOnly, ChangeImpact.Unknown,
        })
        {
            int count = differences.Count(difference => difference.Impact == impact);

            grid.AddRow(
                "[grey]" + Label(impact) + "[/]",
                count == 0 ? "[grey]0[/]" : Emphasise(impact, count));
        }

        _console.Write(new Panel(grid)
            .Header("[bold]Ce que le fichier compagnon change[/]")
            .Border(BoxBorder.Rounded));

        foreach (Artifact artifact in artifacts)
        {
            _console.MarkupLine(
                "[grey]rapport écrit :[/] " + Markup.Escape(Path.GetFileName(artifact.Path))
                + " [grey](" + artifact.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " octets)[/]");
        }

        _console.WriteLine();
    }

    private void RenderPaged(
        IReadOnlyList<Difference> differences,
        string title,
        CancellationToken cancellationToken)
    {
        if (differences.Count == 0)
        {
            _console.MarkupLine("[green]Aucun.[/]");
            return;
        }

        int page = 0;
        int pages = (differences.Count + PageSize - 1) / PageSize;

        while (!cancellationToken.IsCancellationRequested)
        {
            _console.Write(new Rule("[bold]" + Markup.Escape(title) + "[/]").LeftJustified());

            foreach (Difference difference in differences.Skip(page * PageSize).Take(PageSize))
            {
                Render(difference);
            }

            _console.MarkupLine(
                $"[grey]page {page + 1} / {pages}[/]");

            List<string> choices = [];

            if (page + 1 < pages)
            {
                choices.Add("Page suivante");
            }

            if (page > 0)
            {
                choices.Add("Page précédente");
            }

            choices.Add(NavigationLabels.Back);

            string chosen = _console.Prompt(new SelectionPrompt<string>().AddChoices(choices));

            if (string.Equals(chosen, "Page suivante", StringComparison.Ordinal))
            {
                page++;
                continue;
            }

            if (string.Equals(chosen, "Page précédente", StringComparison.Ordinal))
            {
                page--;
                continue;
            }

            return;
        }
    }

    /// <summary>Écrit une différence : d'abord ce qu'elle fait, ensuite où elle est.</summary>
    /// <remarks>
    /// L'identifiant du changement vient en dernier et en gris. Il sert à retrouver la ligne dans le
    /// rapport, ce qui en fait une référence et non une explication : « PARAMETER-REQUIRED-ADDED » en
    /// tête de ligne n'apprend rien à qui lit l'écran.
    /// </remarks>
    private void Render(Difference difference)
    {
        _console.MarkupLine(
            Emphasise(difference.Impact, Tag(difference.Impact)) + " "
            + Markup.Escape(difference.Location.Display));

        _console.MarkupLine("  " + Markup.Escape(difference.Rationale));

        if (difference.Before is { } before && difference.After is { } after)
        {
            _console.MarkupLine(
                "  [grey]avant[/] " + Markup.Escape(before.Text)
                + " [grey]· maintenant[/] " + Markup.Escape(after.Text));
        }

        _console.MarkupLine(
            "  [grey]" + Markup.Escape(difference.Location.Json) + " · "
            + Markup.Escape(difference.ChangeId) + "[/]");
    }

    /// <summary>La forme courte d'une catégorie, pour tenir en tête de ligne.</summary>
    private static string Tag(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking => "CASSE",
        ChangeImpact.NonBreaking => "compatible",
        ChangeImpact.DocumentationOnly => "texte",
        _ => "à relire",
    };

    /// <summary>
    /// Met en valeur sans que la couleur porte seule le sens : le mot est là pour un terminal qui n'a pas
    /// de couleurs.
    /// </summary>
    private static string Emphasise(ChangeImpact impact, object value) => impact switch
    {
        ChangeImpact.Breaking => "[red]" + Markup.Escape(value.ToString() ?? string.Empty) + "[/]",
        ChangeImpact.NonBreaking => "[green]" + Markup.Escape(value.ToString() ?? string.Empty) + "[/]",
        ChangeImpact.Unknown => "[yellow]" + Markup.Escape(value.ToString() ?? string.Empty) + "[/]",
        _ => "[grey]" + Markup.Escape(value.ToString() ?? string.Empty) + "[/]",
    };

    private static string Label(VersionImpact increment) => increment switch
    {
        VersionImpact.Major => "majeur",
        VersionImpact.Minor => "mineur",
        VersionImpact.Patch => "correctif",
        _ => "aucun",
    };

    /// <summary>Le nom d'une catégorie de changement, tel qu'il s'affiche.</summary>
    /// <remarks>
    /// « cassants » et « inconnus » étaient justes et muets. Ce qui intéresse le lecteur n'est pas la
    /// catégorie mais sa conséquence : est-ce que ce que j'ai déjà déployé continue de fonctionner ?
    /// </remarks>
    private static string Label(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking => "casse les appels existants",
        ChangeImpact.NonBreaking => "sans effet sur les appels existants",
        ChangeImpact.DocumentationOnly => "texte seul",
        _ => "à relire, non tranché",
    };
}
