using DevToolbox.Tools.ApiDelta.Core.Operations;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce que le développeur a retenu pour entrer en production.</summary>
/// <param name="Routes">Les routes à porter entières, celles que la production n'a pas.</param>
/// <param name="Differences">Les différences retenues une par une, sur des routes déjà portées.</param>
/// <remarks>
/// Les deux ensembles sont indépendants et peuvent être tous deux non vides : demander quatre routes
/// nouvelles et trois changements sur une cinquième est une demande ordinaire, pas un cas limite.
/// <para>
/// Invariant, hérité de <see cref="RunSelection"/> : rien ici ne peut atteindre le contenu d'un document.
/// Il n'existe aucun chemin d'ici vers un nœud ni vers une valeur, et c'est ce qui autorise la projection
/// dans un point de reprise.
/// </para>
/// </remarks>
public sealed record CorrectionSelection(
    IReadOnlyList<OperationSelector>? Routes = null,
    IReadOnlyList<DifferenceSelector>? Differences = null)
{
    /// <summary>Les routes retenues, jamais nulles.</summary>
    public IReadOnlyList<OperationSelector> Routes { get; init; } = Routes ?? [];

    /// <summary>Les différences retenues, jamais nulles.</summary>
    public IReadOnlyList<DifferenceSelector> Differences { get; init; } = Differences ?? [];

    /// <summary>Indique que le développeur n'a rien retenu.</summary>
    /// <remarks>
    /// Une sélection vide est une demande de ne rien écrire, jamais une demande de tout écrire. Lire le
    /// silence comme un accord global serait la seule façon pour cet outil de modifier une production que
    /// personne ne lui avait désignée.
    /// </remarks>
    public bool IsEmpty => Routes.Count == 0 && Differences.Count == 0;

    /// <summary>Le nombre d'éléments retenus.</summary>
    public int Count => Routes.Count + Differences.Count;

    /// <summary>Indique si une route est retenue entière.</summary>
    /// <param name="route">La route.</param>
    /// <returns><see langword="true"/> si elle figure parmi les routes retenues.</returns>
    public bool Carries(OperationSelector route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return Routes.Contains(route);
    }

    /// <summary>Les différences retenues sur une route donnée.</summary>
    /// <param name="route">La route.</param>
    /// <returns>Les différences retenues, éventuellement aucune.</returns>
    public IReadOnlyList<DifferenceSelector> On(OperationSelector route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return [.. Differences.Where(difference => difference.Route == route)];
    }
}
