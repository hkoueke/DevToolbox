using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Une route du candidat, telle que l'écran de sélection la montre.</summary>
/// <param name="Selector">Ce que le développeur retiendra.</param>
/// <param name="State">Ce qu'elle vaut du point de vue de la production.</param>
/// <param name="Summary">Le résumé, quand le candidat en donne un.</param>
/// <param name="Differences">Les différences qu'elle porte, chacune sélectionnable à part.</param>
/// <remarks>
/// Les différences voyagent ici pour que l'écran puisse montrer, sous chaque route, ce que la retenir
/// apporterait — avec l'effet et la confiance que la comparaison a établis. Choisir sans cela reviendrait
/// à choisir à l'aveugle, et l'effet établi est précisément ce que la comparaison existe pour produire.
/// </remarks>
public sealed record CorrectionRoute(
    OperationSelector Selector,
    RouteState State,
    string? Summary,
    IReadOnlyList<Difference>? Differences = null)
{
    /// <summary>Les différences, jamais nulles.</summary>
    public IReadOnlyList<Difference> Differences { get; init; } = Differences ?? [];

    /// <summary>L'effet le plus grave que retenir cette route emporterait.</summary>
    /// <remarks>
    /// Par un rang explicite, et non par l'ordre de l'énumération : <see cref="ChangeImpact.Breaking"/>
    /// vaut zéro, si bien qu'un maximum naïf désignerait <see cref="ChangeImpact.Unknown"/> comme le pire
    /// des effets et masquerait la rupture derrière le doute.
    /// </remarks>
    public ChangeImpact Worst => Differences.Count == 0
        ? ChangeImpact.NonBreaking
        : Differences.MinBy(difference => Gravity(difference.Impact))!.Impact;

    /// <summary>Indique que retenir cette route n'apporterait rien.</summary>
    public bool NothingToCarry => State is RouteState.Identical;

    /// <summary>Ce que retenir cette route apporterait, dit en une ligne.</summary>
    public string Brings => State switch
    {
        RouteState.Absent => "route absente de la production",
        RouteState.Differs => Differences.Count == 1
            ? "1 différence"
            : Differences.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " différences",
        _ => "rien à reporter",
    };

    /// <summary>La ligne affichée.</summary>
    public string Display => string.IsNullOrWhiteSpace(Summary)
        ? Selector.Display + " — " + Brings
        : Selector.Display + " — " + Summary + " — " + Brings;

    /// <summary>Le rang d'un effet, du plus grave au moins grave.</summary>
    private static int Gravity(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking => 0,
        ChangeImpact.Unknown => 1,
        ChangeImpact.NonBreaking => 2,
        _ => 3,
    };
}
