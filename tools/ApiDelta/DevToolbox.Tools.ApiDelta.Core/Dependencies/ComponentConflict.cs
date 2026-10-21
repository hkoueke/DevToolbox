using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>Un composant qui porte le même nom des deux côtés sans dire la même chose.</summary>
/// <param name="Name">Le nom partagé.</param>
/// <param name="Kind">La famille de composants.</param>
/// <param name="CandidateLocation">Où il se trouve dans le candidat.</param>
/// <param name="ProductionLocation">Où il se trouve dans la production.</param>
/// <param name="DifferingFields">Les champs qui diffèrent, nommés.</param>
/// <remarks>
/// Rapporté, jamais résolu. Écraser le composant de production parce que le candidat en porte un du même
/// nom est exactement la décision qu'un outil ne doit pas prendre : les deux descriptions peuvent avoir
/// raison chacune de son côté, et c'est un désaccord entre deux équipes, non une erreur à corriger.
/// </remarks>
public sealed record ComponentConflict(
    string Name,
    string Kind,
    SpecPointer CandidateLocation,
    SpecPointer ProductionLocation,
    IReadOnlyList<string> DifferingFields);
