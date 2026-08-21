using DevToolbox.Tools.VarCompare.Core.Comparison;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Affiche le détail d'une variable groupe par groupe : le seul endroit où une valeur soit jamais montrée.
/// </summary>
public sealed class VariableDetailRenderer
{
    /// <summary>Quelle part d'une valeur est montrée avant abrègement.</summary>
    public const int MaxValueLength = 120;

    private readonly IAnsiConsole _console;
    private readonly SymbolSet _symbols;

    /// <summary>Crée le moteur de rendu.</summary>
    /// <param name="console">La console de rendu.</param>
    /// <param name="symbols">Le jeu de symboles adapté aux capacités du terminal.</param>
    public VariableDetailRenderer(IAnsiConsole console, SymbolSet symbols)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(symbols);

        _console = console;
        _symbols = symbols;
    }

    /// <summary>Affiche le détail.</summary>
    /// <param name="detail">La variable à décrire.</param>
    public void Render(VariableDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]" + Markup.Escape(detail.DisplayName) + "[/]")
            .AddColumn(new TableColumn("[bold]Group[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]State[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Value[/]"));

        foreach (VariableDetailEntry entry in detail.PerGroup)
        {
            table.AddRow(
                Markup.Escape(entry.GroupName),
                FormatState(entry),
                FormatValue(entry));
        }

        _console.Write(table);
        _console.MarkupLine("[grey]Values are shown here only, never in the comparison.[/]");
    }

    /// <summary>
    /// Met en forme une valeur de façon qu'elle ne puisse pas perturber l'affichage, et signale son
    /// abrègement le cas échéant.
    /// </summary>
    /// <param name="value">La valeur brute.</param>
    /// <returns>Un texte sûr pour le balisage, sur une seule ligne et de longueur bornée.</returns>
    /// <remarks>
    /// Les sauts de ligne, retours chariot et tabulations sont remplacés plutôt qu'échappés : une valeur sur
    /// plusieurs lignes casserait sinon l'alignement des lignes du tableau.
    /// </remarks>
    public static string Shorten(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string flattened = value
            .Replace("\r\n", "⏎", StringComparison.Ordinal)
            .Replace('\n', '⏎')
            .Replace('\r', '⏎')
            .Replace('\t', ' ');

        return flattened.Length <= MaxValueLength
            ? flattened
            : string.Concat(flattened.AsSpan(0, MaxValueLength), "… (shortened)");
    }

    private string FormatState(VariableDetailEntry entry) =>
        $"[{SymbolSet.Colour(entry.State)}]{_symbols.Symbol(entry.State)} "
        + $"{SymbolSet.Text(entry.State)}[/]";

    private static string FormatValue(VariableDetailEntry entry)
    {
        if (entry.State == CellState.Absent)
        {
            return "[grey]—[/]";
        }

        if (entry.ExistsButUnreadable)
        {
            // Jamais un substitut qu'on pourrait prendre pour le contenu lui-même.
            return "[blue]value exists — not retrievable[/]";
        }

        return entry.Value is { Length: 0 }
            ? "[yellow](empty)[/]"
            : Markup.Escape(Shorten(entry.Value ?? string.Empty));
    }
}
