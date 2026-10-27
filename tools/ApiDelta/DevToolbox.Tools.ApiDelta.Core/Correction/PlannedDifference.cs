using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Une différence que le plan portera, avec ce que la comparaison en avait établi.</summary>
/// <param name="Route">La route sur laquelle elle s'applique.</param>
/// <param name="ChangeId">L'identifiant de la sorte de changement.</param>
/// <param name="Location">Son emplacement dans la description.</param>
/// <param name="Impact">Ce qu'elle fait aux appelants existants.</param>
/// <param name="Confidence">À quel point l'outil en répond.</param>
/// <param name="Rationale">Pourquoi elle est classée ainsi.</param>
/// <remarks>
/// Distincte de <see cref="DifferenceSelector"/>, qui ne porte que de quoi retrouver la différence et
/// entre dans un point de reprise. Celle-ci porte en plus ce qu'il faut pour l'annoncer, et ne survit
/// donc pas au processus.
/// </remarks>
public sealed record PlannedDifference(
    OperationSelector Route,
    string ChangeId,
    SpecPointer Location,
    ChangeImpact Impact,
    Confidence Confidence,
    string Rationale)
{
    /// <summary>La ligne affichée.</summary>
    public string Display => Route.Display + " · " + ChangeId + " · " + Location.Display;
}
