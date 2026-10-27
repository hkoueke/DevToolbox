using DevToolbox.Tools.ApiDelta.Core.Correspondence;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Présente une correspondance proposée, pour confirmation ou correction.</summary>
/// <remarks>
/// Le seul port de cette tranche qui parle à quelqu'un, et le seul par lequel une correspondance peut
/// devenir confirmée. Rien d'autre dans le code n'appelle <c>Confirm</c> : une confirmation qui pourrait
/// venir d'ailleurs ferait de WR-104 une intention plutôt qu'une règle.
/// <para>
/// Renoncer est une réponse, et non une erreur. L'implémentation rend alors la correspondance non
/// confirmée, la séance réussit n'ayant rien comparé, et le rapport dit pourquoi. Traiter le renoncement
/// comme un échec pousserait un développeur à confirmer ce qu'il n'a pas lu, ce qui est précisément le
/// défaut que tout ce mécanisme existe pour empêcher.
/// </para>
/// </remarks>
public interface ICorrespondencePrompt
{
    /// <summary>Présente la correspondance et rend ce que le développeur a décidé.</summary>
    /// <param name="proposed">La correspondance proposée.</param>
    /// <param name="routes">Les routes de la production, pour corriger une ligne.</param>
    /// <param name="cancellationToken">Annule l'invite.</param>
    /// <returns>
    /// La correspondance confirmée, ou la même non confirmée si le développeur a renoncé.
    /// </returns>
    Task<OperationCorrespondence> ConfirmAsync(
        OperationCorrespondence proposed,
        IReadOnlyList<string> routes,
        CancellationToken cancellationToken);
}
