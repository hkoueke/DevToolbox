using DevToolbox.Tools.ApiDelta.Core.Audit;

namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Un constat mis en sourdine, et la décision qui le tait.</summary>
/// <param name="Finding">Le constat, intact.</param>
/// <param name="Suppression">La mise en sourdine qui l'a apparié.</param>
/// <remarks>
/// Le constat est conservé en entier, parce qu'il sera affiché. Une sourdine le soustrait au seuil
/// d'échec, non au lecteur.
/// </remarks>
public sealed record SuppressedFinding(Finding Finding, Suppression Suppression);
