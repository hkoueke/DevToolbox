using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Rassemble ce dont un ensemble d'objets a besoin pour être compréhensible seul.</summary>
public interface IDependencyResolver
{
    /// <summary>Ferme transitivement les dépendances d'un ensemble de racines.</summary>
    /// <param name="roots">Les emplacements de départ, dans le candidat.</param>
    /// <param name="candidate">La description d'où viennent les racines.</param>
    /// <param name="production">La description de référence, pour reconnaître les homonymes.</param>
    /// <param name="cancellationToken">Annule la traversée.</param>
    /// <returns>La fermeture, ou un échec déjà affichable.</returns>
    Result<DependencyClosure> Close(
        IReadOnlyList<SpecPointer> roots,
        SpecDocument candidate,
        SpecDocument production,
        CancellationToken cancellationToken);
}
