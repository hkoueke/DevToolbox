using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>Tout ce dont les objets d'un correctif ont besoin pour tenir debout.</summary>
/// <param name="Required">
/// Les composants à emporter, dans un ordre stable. Fermés transitivement : un schéma qui en référence un
/// autre amène l'autre.
/// </param>
/// <param name="ExternalPreserved">Les références laissées hors du document, telles qu'elles étaient.</param>
/// <param name="Cycles">Les cycles rencontrés, légaux entre schémas.</param>
/// <param name="Conflicts">Les composants homonymes qui ne disent pas la même chose.</param>
public sealed record DependencyClosure(
    IReadOnlyList<SpecPointer> Required,
    IReadOnlyList<ReferenceTarget> ExternalPreserved,
    IReadOnlyList<ReferenceTarget> Cycles,
    IReadOnlyList<ComponentConflict> Conflicts)
{
    /// <summary>Une fermeture vide.</summary>
    public static DependencyClosure Empty { get; } = new([], [], [], []);
}
