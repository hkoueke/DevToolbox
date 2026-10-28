using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Ce que le développeur a arrêté pour une opération du contrat.</summary>
/// <param name="Operation">Le nom de l'opération. La clé de cet enregistrement.</param>
/// <param name="Path">Un gabarit de chemin OpenAPI, déjà validé par <see cref="RouteSyntax" />.</param>
/// <param name="Method">La méthode HTTP.</param>
/// <param name="Placements">Chaque champ de l'entrée, placé ou écarté. Jamais partiel.</param>
/// <param name="Headers">Les composants d'en-tête acceptés, par leur nom de composant.</param>
/// <param name="Insertion">Où la route rejoint le document de production.</param>
/// <param name="Suggestion">Ce que valait la proposition pré-remplie.</param>
/// <param name="TookSuggestion">Si le développeur l'a acceptée sans la changer.</param>
/// <remarks>
/// <paramref name="Suggestion" /> et <paramref name="TookSuggestion" /> ne servent pas à l'écriture : ils
/// servent au rapport, qui doit permettre à un lecteur de distinguer une décision d'une devinette. Une
/// route dont la suggestion a été reprise telle quelle n'a pas la même valeur qu'une route que quelqu'un
/// a tapée.
/// </remarks>
public sealed record AuthoredRoute(
    string Operation,
    string Path,
    HttpOperationMethod Method,
    IReadOnlyList<FieldPlacement> Placements,
    IReadOnlyList<string> Headers,
    InsertionPoint Insertion,
    RouteDerivationKind Suggestion,
    bool TookSuggestion)
{
    /// <summary>Les placements, jamais nuls.</summary>
    public IReadOnlyList<FieldPlacement> Placements { get; init; } = Placements ?? [];

    /// <summary>Les en-têtes, jamais nuls.</summary>
    public IReadOnlyList<string> Headers { get; init; } = Headers ?? [];

    /// <summary>La route telle qu'une sélection la désigne.</summary>
    public OperationSelector Selector => new(Path, Method);

    /// <summary>Ce que la route dit, en une ligne.</summary>
    public string Display => Method.ToString().ToUpperInvariant() + " " + Path;

    /// <summary>Les champs placés ailleurs que nulle part.</summary>
    public IEnumerable<FieldPlacement> Placed =>
        Placements.Where(placement => !placement.IsDropped);

    /// <summary>Les champs écartés sciemment.</summary>
    public IEnumerable<FieldPlacement> Dropped =>
        Placements.Where(placement => placement.IsDropped);

    /// <summary>Les champs qui voyagent dans le corps.</summary>
    public IEnumerable<FieldPlacement> InBody =>
        Placements.Where(placement => placement.Location is ParameterLocation.Body);

    /// <summary>Les champs qui deviennent des paramètres.</summary>
    public IEnumerable<FieldPlacement> AsParameters => Placements.Where(placement =>
        placement.Location is ParameterLocation.Path or ParameterLocation.Query
            or ParameterLocation.Header);

    /// <summary>Ce que valait la suggestion, dans les mots du développeur.</summary>
    public string Worth => Suggestion switch
    {
        RouteDerivationKind.Derived => "dérivée",
        RouteDerivationKind.Partial => "partielle",
        RouteDerivationKind.Fallback => "repli",
        _ => throw new InvalidOperationException("Origine de suggestion inconnue."),
    };
}
