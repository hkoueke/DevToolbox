using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Affiche la comparaison dans la disposition qui convient, avec sa légende et ses décomptes.
/// </summary>
/// <remarks>
/// Aucune cellule ne peut contenir une valeur de variable, puisque <see cref="ComparisonCell"/> n'en porte
/// aucune. C'est ce qui rend cette garantie structurelle plutôt qu'affaire de convention d'affichage.
/// </remarks>
public sealed class ComparisonPresenter : IComparisonPresenter
{
    private readonly IAnsiConsole _console;
    private readonly ConsoleCapabilities _capabilities;
    private readonly SymbolSet _symbols;
    private readonly StackedRenderer _stacked;

    /// <summary>Crée le présentateur.</summary>
    /// <param name="console">La console de rendu.</param>
    /// <param name="capabilities">Renseigne sur ce que le terminal sait afficher.</param>
    public ComparisonPresenter(IAnsiConsole console, ConsoleCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(capabilities);

        _console = console;
        _capabilities = capabilities;
        _symbols = new SymbolSet(capabilities.SupportsUnicode);
        _stacked = new StackedRenderer(console, _symbols);
    }

    /// <inheritdoc />
    public void Render(VariableComparison comparison, ComparisonViewOptions options) =>
        RenderPage(comparison, options, pageNumber: 1);

    /// <summary>Affiche une page de la comparaison.</summary>
    /// <param name="comparison">La comparaison à afficher.</param>
    /// <param name="options">Choix de disposition et de filtrage.</param>
    /// <param name="pageNumber">La page à montrer, à partir de un.</param>
    /// <returns>La page affichée, pour que l'appelant puisse proposer la navigation.</returns>
    public ComparisonPage RenderPage(
        VariableComparison comparison,
        ComparisonViewOptions options,
        int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(options);

        LayoutMetrics metrics = Measure(comparison);

        LayoutDecision decision =
            LayoutSelector.Decide(options.Layout, comparison, _capabilities.Width, metrics);

        if (decision.WarnBeforeRendering && decision.Reason is not null)
        {
            // Avertir avant que la disposition ne change sous les yeux du développeur.
            _console.MarkupLine("[yellow]" + Markup.Escape(decision.Reason) + "[/]");
            _console.WriteLine();
        }

        if (!LayoutSelector.CanShowEverything(decision.Layout, comparison, _capabilities.Width, metrics))
        {
            RenderCannotShowEverything();
            return new ComparisonPage([], 0, 0, comparison.Rows.Count, 1, 1);
        }

        IReadOnlyList<ComparisonRow> rows = comparison.VisibleRows(options.DifferencesOnly);
        int pageSize = ComparisonPager.PageSizeFor(_capabilities.Height, decision.Layout);
        ComparisonPage page = ComparisonPager.Page(rows, pageNumber, pageSize);

        if (decision.Layout == ComparisonLayout.Stacked)
        {
            _stacked.Render(comparison, page.Rows);
        }
        else
        {
            RenderGrid(comparison, page.Rows);
        }

        RenderCounts(comparison, options, page);
        RenderCasingWarning(comparison);
        RenderLegend();

        return page;
    }

    /// <inheritdoc />
    public void RenderLegend()
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        foreach (CellState state in Enum.GetValues<CellState>())
        {
            grid.AddRow(
                $"[{SymbolSet.Colour(state)}]{_symbols.Symbol(state)} {SymbolSet.Text(state)}[/]",
                DescribeState(state));
        }

        grid.AddRow(
            $"[magenta]{Markup.Escape(SymbolSet.KeyVaultQualifier)}[/]", "issue d'un coffre de clés");
        grid.AddRow(
            $"[dim]{Markup.Escape(SymbolSet.ReadOnlyQualifier)}[/]",
            "marquée en lecture seule dans ce groupe");
        grid.AddRow(_symbols.DiffersMarker, "présente partout, les valeurs lisibles diffèrent");
        grid.AddRow(SymbolSet.CasingMarker, "les groupes n'orthographient pas le nom pareil");

        _console.Write(new Panel(grid).Header("[bold]Légende[/]").Border(BoxBorder.Rounded));
    }

    /// <summary>
    /// Mesure la place que prendra chaque colonne, pour que la grille reste la disposition retenue chaque
    /// fois qu'elle tient.
    /// </summary>
    /// <remarks>
    /// La mesure porte sur toute la comparaison et non sur la page affichée : une disposition qui changerait
    /// d'une page à l'autre serait plus déroutante que quelques colonnes de trop.
    /// </remarks>
    /// <param name="comparison">La comparaison à mesurer.</param>
    /// <returns>Les largeurs de contenu, colonne par colonne.</returns>
    private LayoutMetrics Measure(VariableComparison comparison)
    {
        int[] groupColumns = new int[comparison.Groups.Count];

        for (int index = 0; index < comparison.Groups.Count; index++)
        {
            // L'en-tête porte le nom du groupe : la colonne ne peut pas être plus étroite que lui.
            groupColumns[index] = Width(GroupHeader(comparison.Groups[index]));
        }

        int nameColumn = 0;

        foreach (ComparisonRow row in comparison.Rows)
        {
            nameColumn = Math.Max(nameColumn, Width(FormatName(row)));

            for (int index = 0; index < row.Cells.Count && index < groupColumns.Length; index++)
            {
                groupColumns[index] = Math.Max(groupColumns[index], Width(FormatCell(row.Cells[index])));
            }
        }

        return new LayoutMetrics(nameColumn, groupColumns);
    }

    /// <summary>
    /// La largeur à l'écran d'un texte balisé, c'est-à-dire une fois ses balises retirées.
    /// </summary>
    /// <param name="markup">Le texte tel qu'il sera écrit, balises comprises.</param>
    /// <returns>Un nombre de colonnes de terminal.</returns>
    private static int Width(string markup) => Markup.Remove(markup).Length;

    private void RenderGrid(VariableComparison comparison, IReadOnlyList<ComparisonRow> rows)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Variable[/]").NoWrap());

        foreach (VariableGroupSummary group in comparison.Groups)
        {
            table.AddColumn(new TableColumn(GroupHeader(group)).NoWrap());
        }

        foreach (ComparisonRow row in rows)
        {
            table.AddRow(BuildRowCells(row));
        }

        _console.Write(table);
    }

    private void RenderCannotShowEverything()
    {
        // Aucune disposition ne peut abandonner un groupe ou une ligne. Quand tout ne peut pas être montré,
        // le dire est la seule option honnête : une comparaison partielle serait lue comme complète.
        _console.MarkupLine(
            "[red]Ce terminal est trop étroit pour montrer tous les groupes et toutes les variables sans "
            + "troncature.[/]");
        _console.MarkupLine(
            "[grey]Rien n'est affiché plutôt qu'une comparaison partielle, qui induirait en erreur. "
            + "Élargissez le terminal, puis réessayez.[/]");
    }

    private string[] BuildRowCells(ComparisonRow row)
    {
        List<string> cells = [FormatName(row)];

        foreach (ComparisonCell cell in row.Cells)
        {
            cells.Add(FormatCell(cell));
        }

        return [.. cells];
    }

    private string FormatName(ComparisonRow row)
    {
        string name = Markup.Escape(row.DisplayName);

        if (row.ReadableValuesDiffer)
        {
            name += " " + _symbols.DiffersMarker;
        }

        if (row.HasCasingDivergence)
        {
            name += " " + SymbolSet.CasingMarker;
        }

        return name;
    }

    private string FormatCell(ComparisonCell cell)
    {
        string colour = SymbolSet.Colour(cell.State);
        string body = $"[{colour}]{_symbols.Symbol(cell.State)} {SymbolSet.Text(cell.State)}[/]";

        if (cell.IsKeyVaultSourced)
        {
            body += $" [magenta]{Markup.Escape(SymbolSet.KeyVaultQualifier)}[/]";
        }

        if (cell.IsReadOnly)
        {
            body += $" [dim]{Markup.Escape(SymbolSet.ReadOnlyQualifier)}[/]";
        }

        return body;
    }

    private static string GroupHeader(VariableGroupSummary group)
    {
        string header = "[bold]" + Markup.Escape(group.Name) + "[/]";

        return group.IsKeyVaultBacked
            ? header + $" [magenta]{Markup.Escape(SymbolSet.KeyVaultQualifier)}[/]"
            : header;
    }

    private void RenderCounts(
        VariableComparison comparison,
        ComparisonViewOptions options,
        ComparisonPage page)
    {
        ComparisonSummary summary = comparison.Summary;

        _console.MarkupLine(
            "[grey]"
            + $"{summary.TotalVariables} variables comparées sur {comparison.Groups.Count} groupes · "
            + $"{summary.MissingSomewhere} absentes quelque part · {summary.DifferingValues} divergentes · "
            + $"{summary.SecretCells} secrètes · {summary.KeyVaultSourcedCells} issues d'un coffre de clés"
            + "[/]");

        if (summary.UndeterminedCells > 0)
        {
            _console.MarkupLine($"[grey]{summary.UndeterminedCells} cellule(s) n'ont pas pu être établies.[/]");
        }

        int hidden = comparison.HiddenRowCount(options.DifferencesOnly);

        if (hidden > 0)
        {
            _console.MarkupLine($"[grey]{hidden} ligne(s) identique(s) masquée(s).[/]");
        }

        if (page.IsPaged)
        {
            _console.MarkupLine(
                $"[grey]lignes {page.FirstRowNumber} à {page.LastRowNumber} sur {page.TotalRows} "
                + $"(page {page.PageNumber} sur {page.PageCount})[/]");
        }

        _console.MarkupLine(
            "[grey]au " + comparison.RetrievedAt.ToLocalTime().ToString("u", null) + "[/]");

        if (!_capabilities.SupportsColour)
        {
            _console.MarkupLine(
                "[grey]La couleur n'est pas disponible ; chaque état est nommé en toutes lettres "
                + "ci-dessus.[/]");
        }
    }

    private void RenderCasingWarning(VariableComparison comparison)
    {
        // La plateforme n'y voit qu'une seule variable. Le développeur doit donc savoir que les
        // orthographes divergent, même si la comparaison, elle, est juste.
        IReadOnlyList<ComparisonRow> divergent =
            [.. comparison.Rows.Where(row => row.HasCasingDivergence)];

        if (divergent.Count == 0)
        {
            return;
        }

        _console.MarkupLine(
            $"[yellow]{SymbolSet.CasingMarker} {divergent.Count} variable(s) ne sont pas orthographiées "
            + "pareil d'un groupe à l'autre. Azure DevOps ne les distingue pas : elles se comparent donc "
            + "comme une seule.[/]");

        foreach (ComparisonRow row in divergent.Take(5))
        {
            IEnumerable<string> spellings = row.Cells
                .Where(cell => cell.NameAsStored is not null)
                .Select(cell => cell.NameAsStored!)
                .Distinct(StringComparer.Ordinal);

            _console.MarkupLine("  [grey]" + Markup.Escape(string.Join(" / ", spellings)) + "[/]");
        }
    }

    private static string DescribeState(CellState state) => state switch
    {
        CellState.PresentWithValue => "présente, avec une valeur",
        CellState.PresentEmpty => "présente, valeur vide",
        CellState.PresentSecret => "présente, secrète — la valeur ne peut pas être lue",
        CellState.Absent => "absente de ce groupe",
        _ => "l'état n'a pas pu être établi",
    };
}
