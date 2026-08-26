using DevToolbox.Application.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Abstractions;

/// <summary>
/// Rapproche une sélection mémorisée de ce que le développeur peut lire aujourd'hui, et dit ce qui a été
/// écarté.
/// </summary>
/// <remarks>
/// Le dire est la raison d'être de ce port. Écarter en silence un groupe supprimé depuis, ou devenu
/// illisible, produirait une comparaison amputée sans que rien ne l'annonce : le développeur conclurait
/// d'une colonne manquante que le groupe est vide. Implémenté dans la couche Spectre, qui seule sait écrire.
/// </remarks>
public interface IResumedSelectionReconciler
{
    /// <summary>Écarte les groupes mémorisés qui ne figurent plus parmi les groupes lisibles.</summary>
    /// <param name="remembered">Les groupes que l'exécution interrompue avait retenus.</param>
    /// <param name="available">Les groupes lisibles dans le projet aujourd'hui.</param>
    /// <returns>Les groupes encore comparables, dans l'ordre où ils avaient été retenus.</returns>
    IReadOnlyList<VariableGroupSummary> Reconcile(
        IReadOnlyList<PersistedGroupSelection> remembered,
        IReadOnlyList<VariableGroupSummary> available);
}
