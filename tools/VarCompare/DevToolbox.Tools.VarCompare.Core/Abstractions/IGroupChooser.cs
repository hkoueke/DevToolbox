using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Demande quels groupes de variables comparer, en imposant qu'au moins deux soient retenus. Implémenté
/// dans la couche Spectre.
/// </summary>
public interface IGroupChooser
{
    /// <summary>Retient au moins deux groupes parmi ceux que le développeur peut lire.</summary>
    /// <param name="available">Les groupes disponibles, avec leur marque de coffre de clés.</param>
    /// <param name="cancellationToken">Annule l'invite.</param>
    /// <returns>
    /// Les groupes retenus, ou une liste vide si le développeur est revenu en arrière. Une sélection de
    /// moins de deux groupes est refusée au sein de l'implémentation, en conservant les choix déjà faits.
    /// </returns>
    Task<IReadOnlyList<VariableGroupSummary>> ChooseAsync(
        IReadOnlyList<VariableGroupSummary> available,
        CancellationToken cancellationToken);
}
