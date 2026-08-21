using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Presentation.Rendering;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation;

/// <summary>
/// Pilote la vue de comparaison une fois qu'elle est à l'écran : pagination, filtrage, changement de
/// disposition, ouverture du détail d'une variable et rafraîchissement.
/// </summary>
/// <remarks>
/// La comparaison elle-même est rendue à partir d'objets <see cref="ComparisonRow"/> qui ne portent aucune
/// valeur. Une valeur n'atteint l'écran que par <see cref="OpenDetail"/>, accessible uniquement sur choix
/// explicite.
/// </remarks>
public sealed class ComparisonSession
{
    private const string NextPage = "Page suivante";
    private const string PreviousPage = "Page précédente";
    private const string ShowDifferencesOnly = "N'afficher que les différences";
    private const string ShowAllRows = "Afficher toutes les lignes";
    private const string UseStacked = "Passer à la disposition empilée";
    private const string UseSideBySide = "Passer à la disposition en colonnes";
    private const string OpenVariable = "Ouvrir une variable";
    private const string Refresh = "Rafraîchir (relire les groupes)";
    private const string Back = "Retour au menu";

    private readonly IAnsiConsole _console;
    private readonly ComparisonPresenter _presenter;
    private readonly VariableDetailRenderer _detail;

    private ComparisonViewOptions _options = ComparisonViewOptions.Default;
    private int _pageNumber = 1;

    /// <summary>Crée la session.</summary>
    /// <param name="console">La console de rendu.</param>
    /// <param name="presenter">Affiche la comparaison.</param>
    /// <param name="detail">Affiche le détail d'une variable.</param>
    public ComparisonSession(
        IAnsiConsole console,
        ComparisonPresenter presenter,
        VariableDetailRenderer detail)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(detail);

        _console = console;
        _presenter = presenter;
        _detail = detail;
    }

    /// <summary>Affiche la comparaison et traite les choix du développeur jusqu'à ce qu'il quitte.</summary>
    /// <param name="comparison">La comparaison à afficher.</param>
    /// <param name="snapshots">Les clichés sous-jacents, utilisés seulement pour bâtir un détail à la demande.</param>
    /// <param name="cancellationToken">Annule la boucle.</param>
    /// <returns><see langword="true"/> si le développeur a demandé un rafraîchissement.</returns>
    public bool Show(
        VariableComparison comparison,
        IReadOnlyList<VariableGroupSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(snapshots);

        while (!cancellationToken.IsCancellationRequested)
        {
            _console.Clear();
            ComparisonPage page = _presenter.RenderPage(comparison, _options, _pageNumber);

            RenderReadOnlyPointer(comparison);

            string choice = _console.Prompt(
                new SelectionPrompt<string>()
                    .Title("Et maintenant ?")
                    .AddChoices(BuildChoices(page)));

            if (string.Equals(choice, Back, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(choice, Refresh, StringComparison.Ordinal))
            {
                return true;
            }

            Apply(choice, comparison, snapshots);
        }

        return false;
    }

    private void Apply(
        string choice,
        VariableComparison comparison,
        IReadOnlyList<VariableGroupSnapshot> snapshots)
    {
        switch (choice)
        {
            case NextPage:
                _pageNumber++;
                break;

            case PreviousPage:
                _pageNumber--;
                break;

            case ShowDifferencesOnly:
                _options = _options with { DifferencesOnly = true };
                _pageNumber = 1;
                break;

            case ShowAllRows:
                _options = _options with { DifferencesOnly = false };
                _pageNumber = 1;
                break;

            case UseStacked:
                _options = _options with { Layout = ComparisonLayout.Stacked };
                _pageNumber = 1;
                break;

            case UseSideBySide:
                _options = _options with { Layout = ComparisonLayout.SideBySide };
                _pageNumber = 1;
                break;

            case OpenVariable:
                OpenDetail(comparison, snapshots);
                break;

            default:
                break;
        }
    }

    private List<string> BuildChoices(ComparisonPage page)
    {
        List<string> choices = [];

        if (page.HasNext)
        {
            choices.Add(NextPage);
        }

        if (page.HasPrevious)
        {
            choices.Add(PreviousPage);
        }

        choices.Add(_options.DifferencesOnly ? ShowAllRows : ShowDifferencesOnly);

        // Le changement manuel de disposition reste possible quel que soit le nombre de groupes.
        choices.Add(_options.Layout == ComparisonLayout.Stacked ? UseSideBySide : UseStacked);

        choices.Add(OpenVariable);
        choices.Add(Refresh);
        choices.Add(Back);

        return choices;
    }

    private void OpenDetail(
        VariableComparison comparison,
        IReadOnlyList<VariableGroupSnapshot> snapshots)
    {
        IReadOnlyList<ComparisonRow> rows = comparison.VisibleRows(_options.DifferencesOnly);

        if (rows.Count == 0)
        {
            return;
        }

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Quelle [bold]variable[/] ?")
                .PageSize(15)
                .MoreChoicesText("[grey](déplacez-vous vers le haut ou le bas pour en voir plus)[/]")
                .AddChoices(rows.Select(row => row.DisplayName)));

        ComparisonRow? row = rows.FirstOrDefault(
            candidate => string.Equals(candidate.DisplayName, chosen, StringComparison.Ordinal));

        if (row is null)
        {
            return;
        }

        _console.Clear();
        _detail.Render(VariableDetail.Build(snapshots, row.CanonicalName));

        _console.MarkupLine("[grey]Appuyez sur Entrée pour revenir à la comparaison.[/]");
        _console.Input.ReadKey(intercept: true);
    }

    private void RenderReadOnlyPointer(VariableComparison comparison)
    {
        // Là où une différence apparaît, indiquer où la corriger plutôt que proposer de le faire.
        ComparisonRow? missing = comparison.Rows.FirstOrDefault(row => row.IsMissingSomewhere);

        if (missing is null)
        {
            return;
        }

        int index = missing.Cells
            .Select((cell, position) => (cell, position))
            .First(entry => !entry.cell.IsPresent)
            .position;

        _console.MarkupLine(
            $"[grey]{Markup.Escape(missing.DisplayName)} est absente de "
            + $"« {Markup.Escape(comparison.Groups[index].Name)} ». Cet outil est en lecture seule — "
            + "ajoutez-la dans Azure DevOps : Pipelines > Library > "
            + $"{Markup.Escape(comparison.Groups[index].Name)}[/]");
    }
}
