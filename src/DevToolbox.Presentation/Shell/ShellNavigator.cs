using DevToolbox.Application.Abstractions;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Pilote la navigation dans les menus. Le menu principal propose toujours de quitter, et chaque sous-menu
/// propose toujours de revenir en arrière.
/// </summary>
public sealed class ShellNavigator
{
    private const string ExitLabel = NavigationLabels.Exit;
    private const string BackLabel = NavigationLabels.Back;

    private readonly IAnsiConsole _console;

    /// <summary>Crée le navigateur.</summary>
    /// <param name="console">La console où présenter les invites.</param>
    public ShellNavigator(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Demande dans quel onglet travailler. Renvoie <see langword="null"/> si le développeur quitte.</summary>
    /// <param name="tabs">Les onglets disponibles.</param>
    /// <returns>L'onglet choisi, ou <see langword="null"/> pour quitter.</returns>
    public string? SelectTab(IReadOnlyList<string> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        if (tabs.Count == 0)
        {
            _console.MarkupLine("[yellow]Aucun outil n'est enregistré.[/]");
            return null;
        }

        List<string> choices = [.. tabs, ExitLabel];

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Choisissez une [bold]catégorie[/] :" + NavigationLabels.Hint)
                .WrapAround()
                .AddChoices(choices));

        return string.Equals(chosen, ExitLabel, StringComparison.Ordinal) ? null : chosen;
    }

    /// <summary>Demande quel outil lancer. Renvoie <see langword="null"/> si le développeur revient en arrière.</summary>
    /// <param name="tools">Les outils de l'onglet actif.</param>
    /// <returns>L'outil choisi, ou <see langword="null"/> pour revenir en arrière.</returns>
    public ITool? SelectTool(IReadOnlyList<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.Count == 0)
        {
            _console.MarkupLine("[yellow]Aucun outil dans cette catégorie pour l'instant.[/]");
            return null;
        }

        Dictionary<string, ITool> byLabel = new(StringComparer.Ordinal);

        foreach (ITool tool in tools)
        {
            byLabel[$"{tool.DisplayName} — {tool.Description}"] = tool;
        }

        List<string> choices = [.. byLabel.Keys, BackLabel];

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Choisissez un [bold]outil[/] :" + NavigationLabels.Hint)
                .WrapAround()
                .AddChoices(choices));

        return byLabel.TryGetValue(chosen, out ITool? selected) ? selected : null;
    }
}
