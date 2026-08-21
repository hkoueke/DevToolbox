using DevToolbox.Domain.Results;
using DevToolbox.Tools.VarCompare.Core.Targets;

namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Détermine dans quel projet Azure DevOps se trouve le développeur, lorsqu'il se trouve dans l'un d'eux.
/// </summary>
public interface IWorkingFolderInspector
{
    /// <summary>Déduit la collection et le projet du dépôt distant du dossier de travail.</summary>
    /// <param name="startDirectory">Le point de départ de la remontée dans l'arborescence.</param>
    /// <returns>
    /// L'origine, ou un succès portant <see langword="null"/> s'il n'y a pas de dépôt, pas de dépôt distant
    /// exploitable, plusieurs dépôts distants ambigus, ou un dépôt distant pointant ailleurs que vers le
    /// serveur configuré. Dans chacun de ces cas, l'outil interroge plutôt que de deviner.
    /// </returns>
    Result<RepositoryOrigin?> DetectOrigin(string startDirectory);
}
