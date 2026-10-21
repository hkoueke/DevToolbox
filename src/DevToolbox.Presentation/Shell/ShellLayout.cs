using DevToolbox.Application.Abstractions;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Dessine le cadre de la boîte à outils : une barre d'onglets construite à partir des outils enregistrés,
/// et un pied de page nommant la cible.
/// </summary>
/// <remarks>
/// Les onglets proviennent des valeurs <see cref="ITool.Tab"/> de ce qui est enregistré, jamais d'une liste
/// écrite en dur : c'est ce qui permettra au second outil d'apparaître sans modifier ce fichier.
/// </remarks>
public sealed class ShellLayout
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée le moteur de rendu du cadre.</summary>
    /// <param name="console">La console où écrire.</param>
    public ShellLayout(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Dessine l'en-tête : titre du produit, barre d'onglets et cible en cours de lecture.</summary>
    /// <param name="tools">Les outils enregistrés, dont les onglets composent la barre.</param>
    /// <param name="activeTab">L'onglet actuellement sélectionné.</param>
    /// <param name="target">
    /// La cible à nommer, ou <see langword="null"/> quand il n'y en a pas : tous les outils ne s'adressent
    /// pas à un serveur, et l'en-tête ne doit pas nommer une cible que l'outil choisi ne contactera jamais.
    /// </param>
    public void RenderHeader(
        IReadOnlyList<ITool> tools,
        string? activeTab,
        string? target)
    {
        ArgumentNullException.ThrowIfNull(tools);

        _console.Write(new Rule("[bold]DevToolbox[/]").LeftJustified());

        IReadOnlyList<string> tabs = DistinctTabs(tools);

        if (tabs.Count > 0)
        {
            _console.Write(new Panel(BuildTabStrip(tabs, activeTab))
                .Border(BoxBorder.None)
                .Padding(0, 0, 0, 0));
        }

        if (!string.IsNullOrWhiteSpace(target))
        {
            _console.MarkupLine("[grey]" + Markup.Escape(target) + "[/]");
        }

        _console.WriteLine();
    }

    /// <summary>Les onglets distincts des outils enregistrés, dans l'ordre du premier enregistrement.</summary>
    /// <param name="tools">Les outils enregistrés.</param>
    /// <returns>Les noms d'onglets.</returns>
    public static IReadOnlyList<string> DistinctTabs(IReadOnlyList<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        return tools
            .Select(tool => tool.Tab)
            .Where(tab => !string.IsNullOrWhiteSpace(tab))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static Grid BuildTabStrip(IReadOnlyList<string> tabs, string? activeTab)
    {
        Grid grid = new();

        foreach (string _ in tabs)
        {
            grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        }

        string[] cells = [.. tabs.Select(tab => FormatTab(tab, activeTab))];
        grid.AddRow(cells);

        return grid;
    }

    private static string FormatTab(string tab, string? activeTab) =>
        string.Equals(tab, activeTab, StringComparison.Ordinal)
            ? $"[bold invert] {Markup.Escape(tab)} [/]"
            : $"[grey] {Markup.Escape(tab)} [/]";
}
