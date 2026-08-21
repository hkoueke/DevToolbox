using DevToolbox.Tools.VarCompare.Core.Comparison;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Affiche la comparaison sous forme d'un panneau par variable, ses états dans chaque groupe listés
/// en dessous.
/// </summary>
/// <remarks>
/// Cette disposition porte exactement les mêmes états, symboles et marques que la grille : seule
/// l'organisation change. Son besoin en largeur ne croît pas avec le nombre de groupes, ce qui en fait la
/// réponse honnête lorsque la grille tronquerait.
/// </remarks>
public sealed class StackedRenderer
{
    private readonly IAnsiConsole _console;
    private readonly SymbolSet _symbols;

    /// <summary>Crée le moteur de rendu.</summary>
    /// <param name="console">La console de rendu.</param>
    /// <param name="symbols">Le jeu de symboles adapté aux capacités du terminal.</param>
    public StackedRenderer(IAnsiConsole console, SymbolSet symbols)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(symbols);

        _console = console;
        _symbols = symbols;
    }

    /// <summary>Affiche les lignes données dans la disposition empilée.</summary>
    /// <param name="comparison">La comparaison affichée, pour ses noms de groupes.</param>
    /// <param name="rows">Les lignes à montrer sur cette page.</param>
    public void Render(VariableComparison comparison, IReadOnlyList<ComparisonRow> rows)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(rows);

        foreach (ComparisonRow row in rows)
        {
            _console.Write(new Panel(BuildBody(comparison, row))
                .Header(BuildHeader(row))
                .Border(BoxBorder.Rounded)
                .Expand());
        }
    }

    private Grid BuildBody(VariableComparison comparison, ComparisonRow row)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        for (int index = 0; index < row.Cells.Count; index++)
        {
            ComparisonCell cell = row.Cells[index];
            string groupName = comparison.Groups[index].Name;

            grid.AddRow(
                "[grey]" + Markup.Escape(groupName) + "[/]",
                FormatCell(cell));
        }

        return grid;
    }

    private string BuildHeader(ComparisonRow row)
    {
        string header = "[bold]" + Markup.Escape(row.DisplayName) + "[/]";

        if (row.ReadableValuesDiffer)
        {
            header += " " + _symbols.DiffersMarker;
        }

        if (row.HasCasingDivergence)
        {
            header += " " + SymbolSet.CasingMarker;
        }

        return header;
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
}
