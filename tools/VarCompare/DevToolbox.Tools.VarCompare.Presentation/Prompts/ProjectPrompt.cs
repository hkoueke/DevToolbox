using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Targets;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Prompts;

/// <summary>
/// Confirme un projet détecté, ou propose la liste des projets accessibles.
/// </summary>
public sealed class ProjectPrompt : IProjectChooser
{
    private const string BackLabel = "Back";

    private readonly IAnsiConsole _console;

    /// <summary>Crée l'invite.</summary>
    /// <param name="console">La console où présenter l'invite.</param>
    public ProjectPrompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public Task<ProjectIdentifier?> ChooseAsync(
        RepositoryOrigin? detected,
        IReadOnlyList<ProjectIdentifier> available,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(available);

        if (detected is not null && TryConfirmDetected(detected, out ProjectIdentifier? confirmed))
        {
            return Task.FromResult<ProjectIdentifier?>(confirmed);
        }

        return Task.FromResult(ChooseFromList(available));
    }

    private bool TryConfirmDetected(RepositoryOrigin detected, out ProjectIdentifier? confirmed)
    {
        // Présenté pour confirmation, jamais appliqué en silence. Le dépôt distant est affiché lui aussi,
        // pour que le développeur voie sur quoi la déduction repose.
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow("[grey]collection[/]", Markup.Escape(detected.Collection));
        grid.AddRow("[grey]project[/]", Markup.Escape(detected.Project.Name));
        grid.AddRow("[grey]repository[/]", Markup.Escape(detected.RepositoryName));
        grid.AddRow("[grey]remote[/]", Markup.Escape(detected.RemoteName));
        grid.AddRow("[grey]url[/]", Markup.Escape(detected.SourceUrl));

        _console.Write(new Panel(grid)
            .Header("[bold]Detected from the folder you are in[/]")
            .Border(BoxBorder.Rounded));

        bool accepted = _console.Confirm(
            $"Compare variable groups in [bold]{Markup.Escape(detected.Project.Name)}[/]?");

        confirmed = accepted ? detected.Project : null;

        return accepted;
    }

    private ProjectIdentifier? ChooseFromList(IReadOnlyList<ProjectIdentifier> available)
    {
        if (available.Count == 0)
        {
            _console.MarkupLine("[yellow]You do not have access to any project in this collection.[/]");
            return null;
        }

        Dictionary<string, ProjectIdentifier> byName = new(StringComparer.Ordinal);

        foreach (ProjectIdentifier project in available)
        {
            byName[project.Name] = project;
        }

        List<string> choices = [.. byName.Keys, BackLabel];

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Which [bold]project[/]?")
                .PageSize(15)
                .MoreChoicesText("[grey](move up and down for more)[/]")
                .AddChoices(choices));

        return byName.TryGetValue(chosen, out ProjectIdentifier? selected) ? selected : null;
    }
}
