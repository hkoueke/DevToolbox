using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Extraction;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Une route du candidat, telle que l'écran de sélection la montre.</summary>
/// <param name="Selector">Ce que le développeur retiendra.</param>
/// <param name="State">Ce qu'elle vaut du point de vue de la production.</param>
/// <param name="Summary">Le résumé, quand le candidat en donne un.</param>
/// <param name="Differences">Les différences qu'elle porte, chacune sélectionnable à part.</param>
/// <param name="Coverage">Ce que la production fait de l'opération dont elle est dérivée.</param>
/// <param name="Pairing">L'appariement dont elle dépend, quand il y en a un.</param>
/// <remarks>
/// Les différences voyagent ici pour que l'écran puisse montrer, sous chaque route, ce que la retenir
/// apporterait — avec l'effet et la confiance que la comparaison a établis. Choisir sans cela reviendrait
/// à choisir à l'aveugle, et l'effet établi est précisément ce que la comparaison existe pour produire.
/// <para>
/// La couverture et l'appariement voyagent pour la même raison, et seulement quand le candidat est un
/// contrat de service. Un développeur qui retient une route reposant sur un appariement que l'outil a
/// déduit choisit sur une base plus faible, et il doit l'apprendre <em>au moment de choisir</em> : le
/// lui dire dans le rapport serait le lui dire après.
/// </para>
/// </remarks>
public sealed record CorrectionRoute(
    OperationSelector Selector,
    RouteState State,
    string? Summary,
    IReadOnlyList<Difference>? Differences = null,
    CoverageState? Coverage = null,
    CorrespondenceEntry? Pairing = null)
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

    /// <summary>
    /// Ce sur quoi la retenir reposerait, quand elle vient d'un contrat de service.
    /// </summary>
    /// <remarks>
    /// Vide pour un candidat OpenAPI, qui ne repose sur aucun appariement : la ligne ne gagne alors rien
    /// à dire qu'elle ne repose sur rien.
    /// </remarks>
    public string Footing => string.Concat(Marks());

    /// <summary>La ligne affichée.</summary>
    public string Display => (string.IsNullOrWhiteSpace(Summary)
        ? Selector.Display + " — " + Brings
        : Selector.Display + " — " + Summary + " — " + Brings) + Footing;

    /// <summary>Les marques que la base ajoute à la ligne, dans un ordre fixe.</summary>
    /// <remarks>
    /// L'appariement déduit est dit par « déduit » et non par le silence. Une absence de marque signifie
    /// « déclaré par vous », et c'est le seul des deux sens qu'on peut laisser porter par le défaut : se
    /// taire sur une déduction la ferait passer pour une déclaration.
    /// </remarks>
    private IEnumerable<string> Marks()
    {
        if (Coverage is { } state)
        {
            yield return " · couverture : " + state switch
            {
                CoverageState.Covered => "couverte",
                CoverageState.Absent => "absente de la production",
                _ => "ambiguë",
            };
        }

        if (Pairing is { } paired)
        {
            yield return paired.IsInferred
                ? " · appariement déduit (" + paired.Mark + ")"
                : " · appariement " + paired.Mark;
        }
    }

    /// <summary>Le rang d'un effet, du plus grave au moins grave.</summary>
    private static int Gravity(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking => 0,
        ChangeImpact.Unknown => 1,
        ChangeImpact.NonBreaking => 2,
        _ => 3,
    };
}
