using DevToolbox.Application.Abstractions;
using Spectre.Console;

namespace DevToolbox.Presentation.Shell;

/// <summary>
/// Annonce les reprises que le pipeline de résilience effectue de lui-même.
/// </summary>
/// <remarks>
/// Vit dans la coquille partagée et non dans un outil : les reprises se produisent dans le client HTTP
/// commun, si bien que tout outil qui l'emploie hérite de l'annonce sans l'enregistrer. Les écritures sont
/// sérialisées car les reprises surviennent sur les fils d'exécution du pipeline, plusieurs à la fois.
/// </remarks>
public sealed class SpectreRetryAnnouncer : IRetryObserver
{
    private readonly Lock _gate = new();
    private readonly IAnsiConsole _console;

    /// <summary>Crée l'annonceur.</summary>
    /// <param name="console">La console où écrire.</param>
    public SpectreRetryAnnouncer(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public void Retrying(int attempt, int maxAttempts, TimeSpan delay)
    {
        lock (_gate)
        {
            _console.MarkupLine(
                $"[yellow]nouvel essai[/] [grey]({attempt}/{maxAttempts}, dans "
                + $"{delay.TotalSeconds:F1} s)[/]");
        }
    }

    /// <inheritdoc />
    public void Throttled(TimeSpan retryAfter)
    {
        lock (_gate)
        {
            _console.MarkupLine(
                $"[yellow]Le service limite le débit. Attente de {retryAfter.TotalSeconds:F0} s comme "
                + "demandé, plutôt que de le solliciter sans relâche.[/]");
        }
    }
}
