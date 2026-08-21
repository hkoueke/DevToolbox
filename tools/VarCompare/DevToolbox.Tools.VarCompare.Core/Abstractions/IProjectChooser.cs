using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Tools.VarCompare.Core.Targets;

namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Demande sur quel projet travailler : soit en confirmant celui qui a été détecté, soit en le choisissant
/// dans la liste accessible. Implémenté dans la couche Spectre.
/// </summary>
public interface IProjectChooser
{
    /// <summary>Confirme ou choisit le projet.</summary>
    /// <param name="detected">
    /// Ce que suggère le dossier de travail, ou <see langword="null"/> si rien d'exploitable n'a été
    /// détecté. Une valeur détectée est présentée pour confirmation, jamais appliquée en silence.
    /// </param>
    /// <param name="available">Les projets accessibles au développeur, pour le choix dans une liste.</param>
    /// <param name="cancellationToken">Annule l'invite.</param>
    /// <returns>Le projet choisi, ou <see langword="null"/> si le développeur est revenu en arrière.</returns>
    Task<ProjectIdentifier?> ChooseAsync(
        RepositoryOrigin? detected,
        IReadOnlyList<ProjectIdentifier> available,
        CancellationToken cancellationToken);
}
