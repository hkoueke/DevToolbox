using DevToolbox.Domain.Runs;

namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Demande au développeur quoi faire d'une étape en échec. Implémentée dans la couche Spectre.
/// </summary>
public interface IRecoveryPrompt
{
    /// <summary>Présente les quatre possibilités de reprise pour une étape en échec.</summary>
    /// <param name="failed">L'étape en échec, dont le message est déjà affichable tel quel.</param>
    /// <param name="cancellationToken">Annule l'invite.</param>
    /// <returns>Le choix du développeur.</returns>
    Task<RecoveryChoice> AskAsync(StepState failed, CancellationToken cancellationToken);
}
