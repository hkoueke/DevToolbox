using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Applique les décisions au candidat avant sa comparaison, sans altérer la projection source.</summary>
public static class AuthoredProjection
{
    /// <summary>Compose uniquement les opérations retenues, avec leurs routes et leur origine.</summary>
    /// <param name="projection">La projection source, réutilisable lors d'une reprise.</param>
    /// <param name="authoring">Les décisions validées.</param>
    /// <param name="production">La production, dont les identifiants d'opération doivent rester uniques.</param>
    /// <returns>Le candidat réellement choisi par l'utilisateur.</returns>
    /// <exception cref="InvalidOperationException">Une décision ne peut pas être représentée fidèlement.</exception>
    public static ProjectionResult Apply(ProjectionResult projection, AuthoringRecord authoring, SpecDocument production)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(authoring);
        ArgumentNullException.ThrowIfNull(production);
        HashSet<string> heldIds = OperationIds(production);
        SpecPointer paths = SpecPointer.Root.Child("paths");
        // Une suggestion non retenue ne réserve rien. Les schémas restent disponibles pour la fermeture des dépendances.
        SpecNode root = TreeEdits.Insert(projection.Document.Root, paths, TreeEdits.Mapping(paths, []));
        Dictionary<string, DerivedRoute> routes = new(StringComparer.Ordinal);
        foreach (AuthoredRoute route in authoring.Routes)
        {
            if (!projection.Routes.TryGetValue(route.Operation, out DerivedRoute? derived))
            {
                throw new InvalidOperationException("L'opération « " + route.Operation + " » n'a pas de projection.");
            }

            SpecPointer source = At(derived.Path, derived.Method.ToLowerInvariant());
            SpecNode operation = PointerNavigator.Find(projection.Document.Root, source.Json)
                ?? throw new InvalidOperationException("La route source de « " + route.Operation + " » est introuvable.");
            SpecNode composed = OperationAssembly.Compose(operation, route, route.Headers, projection.Document.Root);
            // L'identifiant est facultatif en OpenAPI ; le dupliquer rendrait le contrat invalide.
            if (composed is MappingNode mapping && mapping.Find("operationId") is ScalarNode id && heldIds.Contains(id.Text))
            {
                composed = TreeEdits.Remove(composed, SpecPointer.Root.Child("operationId"));
            }

            SpecPointer target = At(route.Path, route.Selector.MethodKey);
            if (PointerNavigator.Find(root, target.Json) is not null)
            {
                throw new InvalidOperationException("La route « " + route.Display + " » est déjà occupée dans le candidat.");
            }

            root = TreeEdits.Insert(root, target, composed);
            routes[route.Operation] = derived with { Path = route.Path, Method = route.Method.ToString().ToUpperInvariant() };
        }

        return projection with { Document = projection.Document with { Root = root }, Routes = routes };
    }

    private static SpecPointer At(string path, string method) => SpecPointer.Root.Child("paths").Child(path).Child(method);

    private static HashSet<string> OperationIds(SpecDocument production)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (PointerNavigator.Find(production.Root, "/paths") is not MappingNode paths)
        {
            return ids;
        }

        foreach (MappingNode path in paths.Entries.Select(entry => entry.Value).OfType<MappingNode>())
        {
            foreach (MappingNode operation in path.Entries.Select(entry => entry.Value).OfType<MappingNode>())
            {
                if (operation.Find("operationId") is ScalarNode id)
                {
                    ids.Add(id.Text);
                }
            }
        }

        return ids;
    }
}
