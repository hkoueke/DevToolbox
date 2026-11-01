using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Applique les décisions au candidat avant sa comparaison, sans altérer la projection source.</summary>
public static class AuthoredProjection
{
    /// <summary>Compose les opérations et déplace leurs routes en conservant leur origine.</summary>
    /// <param name="projection">La projection source, réutilisable lors d'une reprise.</param>
    /// <param name="authoring">Les décisions validées.</param>
    /// <returns>Le candidat réellement choisi par l'utilisateur.</returns>
    /// <exception cref="InvalidOperationException">Une décision ne peut pas être représentée fidèlement.</exception>
    public static ProjectionResult Apply(ProjectionResult projection, AuthoringRecord authoring)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(authoring);
        SpecNode root = projection.Document.Root;
        Dictionary<string, DerivedRoute> routes = new(projection.Routes, StringComparer.Ordinal);
        List<(AuthoredRoute Route, SpecNode Operation)> operations = [];
        foreach (AuthoredRoute route in authoring.Routes)
        {
            if (!routes.TryGetValue(route.Operation, out DerivedRoute? derived))
            {
                throw new InvalidOperationException("L'opération « " + route.Operation + " » n'a pas de projection.");
            }

            SpecPointer source = At(derived.Path, derived.Method.ToLowerInvariant());
            SpecNode operation = PointerNavigator.Find(projection.Document.Root, source.Json)
                ?? throw new InvalidOperationException("La route source de « " + route.Operation + " » est introuvable.");
            operations.Add((route, OperationAssembly.Compose(operation, route, route.Headers, projection.Document.Root)));
            root = TreeEdits.Remove(root, source);
            SpecPointer path = SpecPointer.Root.Child("paths").Child(derived.Path);
            if (PointerNavigator.Find(root, path.Json) is MappingNode { Entries.Count: 0 })
            {
                root = TreeEdits.Remove(root, path);
            }
        }

        foreach (var (route, operation) in operations)
        {
            SpecPointer target = At(route.Path, route.Selector.MethodKey);
            if (PointerNavigator.Find(root, target.Json) is not null)
            {
                throw new InvalidOperationException("La route « " + route.Display + " » est déjà occupée dans le candidat.");
            }

            root = TreeEdits.Insert(root, target, operation);
            routes[route.Operation] = routes[route.Operation] with { Path = route.Path, Method = route.Method.ToString().ToUpperInvariant() };
        }

        return projection with { Document = projection.Document with { Root = root }, Routes = routes };
    }

    private static SpecPointer At(string path, string method) => SpecPointer.Root.Child("paths").Child(path).Child(method);
}
