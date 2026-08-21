using DevToolbox.Domain.Runs;

namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Exécute les étapes d'une exécution dans l'ordre, enregistre un point de reprise après chaque succès et
/// propose une reprise en cas d'échec.
/// </summary>
public interface IStepRunner
{
    /// <summary>Déroule les étapes jusqu'à l'aboutissement ou l'abandon.</summary>
    /// <param name="context">Le contexte d'exécution, dont l'exécution est mise à jour sur place.</param>
    /// <param name="steps">Les étapes, dans l'ordre. Elles doivent correspondre aux noms déclarés sur l'exécution.</param>
    /// <param name="cancellationToken">Annule l'exécution de manière coopérative.</param>
    /// <returns>L'exécution, porteuse de son issue finale.</returns>
    Task<ToolRun> RunAsync(
        ToolRunContext context,
        IReadOnlyList<IToolStep> steps,
        CancellationToken cancellationToken);
}
