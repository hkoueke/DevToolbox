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

        LayoutDecision decision = LayoutSelector.Decide(options.Layout, comparison, _capabilities.Width);

        if (decision.WarnBeforeRendering && decision.Reason is not null)
        {
            // Avertir avant que la disposition ne change sous les yeux du développeur.
            _console.MarkupLine("[yellow]" + Markup.Escape(decision.Reason) + "[/]");
            _console.WriteLine();
        }

        if (!LayoutSelector.CanShowEverything(decision.Layout, comparison, _capabilities.Width))
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
            $"[magenta]{Markup.Escape(SymbolSet.KeyVaultQualifier)}[/]", "sourced from a key vault");
        grid.AddRow(
            $"[dim]{Markup.Escape(SymbolSet.ReadOnlyQualifier)}[/]", "marked read-only in that group");
        grid.AddRow(_symbols.DiffersMarker, "present everywhere, readable values differ");
        grid.AddRow(SymbolSet.CasingMarker, "groups spell the name differently");

        _console.Write(new Panel(grid).Header("[bold]Legend[/]").Border(BoxBorder.Rounded));
    }

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
            "[red]This terminal is too narrow to show every group and every variable without clipping.[/]");
        _console.MarkupLine(
            "[grey]Nothing is shown rather than a partial comparison, which would mislead. "
            + "Widen the terminal and try again.[/]");
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
            + $"{summary.TotalVariables} variables compared across {comparison.Groups.Count} groups · "
            + $"{summary.MissingSomewhere} missing somewhere · {summary.DifferingValues} differing · "
            + $"{summary.SecretCells} secret · {summary.KeyVaultSourcedCells} key-vault-sourced"
            + "[/]");

        if (summary.UndeterminedCells > 0)
        {
            _console.MarkupLine($"[grey]{summary.UndeterminedCells} cell(s) could not be determined.[/]");
        }

        int hidden = comparison.HiddenRowCount(options.DifferencesOnly);

        if (hidden > 0)
        {
            _console.MarkupLine($"[grey]{hidden} identical row(s) hidden.[/]");
        }

        if (page.IsPaged)
        {
            _console.MarkupLine(
                $"[grey]rows {page.FirstRowNumber}-{page.LastRowNumber} of {page.TotalRows} "
                + $"(page {page.PageNumber} of {page.PageCount})[/]");
        }

        _console.MarkupLine(
            "[grey]as at " + comparison.RetrievedAt.ToLocalTime().ToString("u", null) + "[/]");

        if (!_capabilities.SupportsColour)
        {
            _console.MarkupLine("[grey]Colour is unavailable; every state is named in words above.[/]");
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
            $"[yellow]{SymbolSet.CasingMarker} {divergent.Count} variable(s) are spelled differently "
            + "between groups. Azure DevOps does not distinguish them, so they compare as one.[/]");

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
        CellState.PresentWithValue => "present, with a value",
        CellState.PresentEmpty => "present, value is empty",
        CellState.PresentSecret => "present, secret — the value cannot be read",
        CellState.Absent => "not present in that group",
        _ => "state could not be determined",
    };
}
