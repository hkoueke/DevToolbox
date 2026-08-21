using DevToolbox.Tools.VarCompare.Core.Abstractions;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Montre quel groupe est en cours de lecture, pour qu'une requête lente ou rejouée paraisse active plutôt
/// que figée.
/// </summary>
public sealed class RetrievalProgress : IRetrievalProgress
{
    private readonly IAnsiConsole _console;
    private int _total;
    private int _done;

    /// <summary>Crée le rapporteur d'avancement.</summary>
    /// <param name="console">La console où écrire.</param>
    public RetrievalProgress(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public void Begin(int total, int alreadyRetrieved)
    {
        _total = total;
        _done = alreadyRetrieved;

        if (alreadyRetrieved > 0)
        {
            // Poursuite après un échec : dire clairement que le travail acquis est réutilisé, pas refait.
            _console.MarkupLine(
                $"[grey]Réutilisation de {alreadyRetrieved} groupe(s) déjà lu(s) dans cette session.[/]");
        }
    }

    /// <inheritdoc />
    public void Retrieving(string groupName) =>
        _console.MarkupLine($"[grey]Lecture de {Markup.Escape(groupName)}…[/]");

    /// <inheritdoc />
    public void Retrieved(string groupName)
    {
        _done++;
        _console.MarkupLine(
            $"[green]✓[/] {Markup.Escape(groupName)} [grey]({_done}/{_total})[/]");
    }

    /// <inheritdoc />
    public void Failed(string groupName, string reason) =>
        _console.MarkupLine($"[red]✗[/] {Markup.Escape(groupName)}: {Markup.Escape(reason)}");

    /// <inheritdoc />
    public void Retrying(string groupName, int attempt, int maxAttempts) =>
        _console.MarkupLine(
            $"[yellow]nouvel essai[/] {Markup.Escape(groupName)} [grey]({attempt}/{maxAttempts})[/]");

    /// <inheritdoc />
    public void Throttled(TimeSpan retryAfter) =>
        _console.MarkupLine(
            $"[yellow]Le service limite le débit. Attente de {retryAfter.TotalSeconds:F0} s comme demandé, "
            + "plutôt que de le solliciter sans relâche.[/]");

    /// <inheritdoc />
    public void Complete() => _console.WriteLine();
}
