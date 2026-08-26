using DevToolbox.Tools.VarCompare.Core.Abstractions;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Rendering;

/// <summary>
/// Montre quel groupe est en cours de lecture, pour qu'une requête lente ou rejouée paraisse active plutôt
/// que figée.
/// </summary>
/// <remarks>
/// Les groupes sont lus en parallèle : chaque méthode est donc appelée depuis plusieurs fils d'exécution à
/// la fois. Le verrou n'est pas une précaution de principe, il est ce qui empêche le compteur de sauter des
/// unités et les lignes de s'entrelacer au milieu d'un balisage.
/// </remarks>
public sealed class RetrievalProgress : IRetrievalProgress
{
    private readonly Lock _gate = new();
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
        lock (_gate)
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
    }

    /// <inheritdoc />
    public void Retrieving(string groupName)
    {
        lock (_gate)
        {
            _console.MarkupLine($"[grey]Lecture de {Markup.Escape(groupName)}…[/]");
        }
    }

    /// <inheritdoc />
    public void Retrieved(string groupName)
    {
        lock (_gate)
        {
            _done++;
            _console.MarkupLine(
                $"[green]✓[/] {Markup.Escape(groupName)} [grey]({_done}/{_total})[/]");
        }
    }

    /// <inheritdoc />
    public void Failed(string groupName, string reason)
    {
        lock (_gate)
        {
            _console.MarkupLine($"[red]✗[/] {Markup.Escape(groupName)}: {Markup.Escape(reason)}");
        }
    }

    /// <inheritdoc />
    public void Complete()
    {
        lock (_gate)
        {
            _console.WriteLine();
        }
    }
}
