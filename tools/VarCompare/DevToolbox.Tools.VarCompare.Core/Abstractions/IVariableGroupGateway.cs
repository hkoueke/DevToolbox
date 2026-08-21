using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Lit les projets et les groupes de variables depuis Azure DevOps.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lecture seule.</b> Aucun membre ici ne peut exprimer une modification, et c'est délibéré : la
/// garantie devient structurelle plutôt que disciplinaire. Rien au-dessus de la couche d'infrastructure ne
/// voit jamais une URL, un code de statut ou une forme JSON.
/// </para>
/// <para>
/// La collection et le serveur proviennent des options validées et non de paramètres : il n'y a qu'une seule
/// cible configurée par exécution.
/// </para>
/// </remarks>
public interface IVariableGroupGateway
{
    /// <summary>Liste les projets visibles par le développeur dans la collection configurée.</summary>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Les projets, ou un échec déjà affichable tel quel.</returns>
    Task<Result<IReadOnlyList<ProjectIdentifier>>> ListProjectsAsync(CancellationToken cancellationToken);

    /// <summary>Liste les groupes de variables lisibles par le développeur dans un projet.</summary>
    /// <param name="project">Le projet à lister.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Les groupes, ou un échec déjà affichable tel quel.</returns>
    Task<Result<IReadOnlyList<VariableGroupSummary>>> ListGroupsAsync(
        ProjectIdentifier project,
        CancellationToken cancellationToken);

    /// <summary>Lit un groupe de variables et son contenu.</summary>
    /// <param name="project">Le projet auquel le groupe appartient.</param>
    /// <param name="groupId">Le groupe à lire.</param>
    /// <param name="cancellationToken">Annule la lecture.</param>
    /// <returns>Le cliché, ou un échec déjà affichable tel quel.</returns>
    Task<Result<VariableGroupSnapshot>> GetGroupAsync(
        ProjectIdentifier project,
        int groupId,
        CancellationToken cancellationToken);
}
