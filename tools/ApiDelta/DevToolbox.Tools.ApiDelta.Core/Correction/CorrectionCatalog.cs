using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>
/// Dresse, route par route, ce que retenir chacune apporterait à la production.
/// </summary>
/// <remarks>
/// Toutes les routes du candidat y figurent, y compris celles qui n'apportent rien. C'est ce qui permet
/// au développeur qui cherche une route de toujours la trouver et de s'y voir dire pourquoi il n'y a rien
/// à reporter, au lieu d'avoir à distinguer lui-même « elle correspondait déjà » de « l'outil l'a
/// manquée ».
/// </remarks>
public static class CorrectionCatalog
{
    private const string Paths = "paths";

    /// <summary>Dresse le catalogue.</summary>
    /// <param name="production">La description en service.</param>
    /// <param name="candidate">La description candidate.</param>
    /// <param name="differences">Ce que la comparaison a trouvé.</param>
    /// <param name="catalog">Énumère les routes d'une description.</param>
    /// <param name="footing">Ce sur quoi la sélection repose, pour un candidat projeté.</param>
    /// <returns>Les routes du candidat, dans l'ordre où le candidat les écrit.</returns>
    public static IReadOnlyList<CorrectionRoute> Build(
        SpecDocument production,
        SpecDocument candidate,
        IReadOnlyList<Difference> differences,
        IOperationCatalog catalog,
        WsdlFooting? footing = null)
    {
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(differences);
        ArgumentNullException.ThrowIfNull(catalog);

        HashSet<OperationSelector> held =
        [
            .. catalog.List(production).Select(operation => operation.Selector),
        ];

        Dictionary<OperationSelector, List<Difference>> attributed = Attribute(differences);
        List<CorrectionRoute> routes = [];

        foreach (SelectableOperation operation in catalog.List(candidate))
        {
            _ = attributed.TryGetValue(operation.Selector, out List<Difference>? own);

            string display = operation.Selector.Display;

            routes.Add(new CorrectionRoute(
                operation.Selector,
                State(held.Contains(operation.Selector), own),
                operation.Summary,
                own ?? [],
                footing?.StateOf(display),
                footing?.PairedWith(display)));
        }

        return routes;
    }

    /// <summary>Ce qu'une route vaut : la production la porte-t-elle, et le candidat en dit-il autre chose.</summary>
    private static RouteState State(bool held, List<Difference>? differences)
    {
        if (!held)
        {
            return RouteState.Absent;
        }

        return differences is { Count: > 0 } ? RouteState.Differs : RouteState.Identical;
    }

    /// <summary>
    /// Rattache chaque différence à la route qu'un appelant la ressentirait à travers.
    /// </summary>
    /// <remarks>
    /// Une différence située sous un composant ne relève d'aucune route : un appelant n'appelle pas un
    /// schéma. Elle est donc portée par la fermeture de dépendances de la route qui l'atteint, et non
    /// présentée comme sélectionnable à part — la sélectionner seule produirait un composant que rien
    /// n'utilise.
    /// </remarks>
    private static Dictionary<OperationSelector, List<Difference>> Attribute(
        IReadOnlyList<Difference> differences)
    {
        Dictionary<OperationSelector, List<Difference>> attributed = [];

        foreach (Difference difference in differences)
        {
            if (Route(difference.Location) is not { } route)
            {
                continue;
            }

            if (!attributed.TryGetValue(route, out List<Difference>? own))
            {
                own = [];
                attributed[route] = own;
            }

            own.Add(difference);
        }

        return attributed;
    }

    /// <summary>La route qu'un emplacement désigne, quand il en désigne une.</summary>
    private static OperationSelector? Route(SpecPointer location)
    {
        IReadOnlyList<string> segments = location.Segments;

        if (segments.Count < 3 || !string.Equals(segments[0], Paths, StringComparison.Ordinal))
        {
            return null;
        }

        return Enum.TryParse(segments[2], ignoreCase: true, out HttpOperationMethod method)
            ? new OperationSelector(segments[1], method)
            : null;
    }
}
