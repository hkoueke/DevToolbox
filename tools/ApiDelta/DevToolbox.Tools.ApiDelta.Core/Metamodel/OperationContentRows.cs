using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Les champs des paramètres, corps de requête, réponses, types de média, en-têtes et liens.
/// </summary>
internal static class OperationContentRows
{
    internal static void Register(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        Parameters(rows);
        Headers(rows);
        Bodies(rows);
        Responses(rows);
        Links(rows);
    }

    private static void Parameters(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Parameter, "name")] = FieldDescriptor.Contractual;

        rows[(SpecObjectKind.Parameter, "in")] = new FieldDescriptor(
            ValueRule: ValueAwareRule.ParameterLocation,
            WhenAdded: ChangeImpact.Breaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Breaking);

        foreach ((string field, FieldDescriptor descriptor) in SharedParameterFields())
        {
            rows[(SpecObjectKind.Parameter, field)] = descriptor;
        }
    }

    /// <summary>
    /// Les champs communs à un paramètre et à un en-tête.
    /// </summary>
    /// <remarks>
    /// Un en-tête de réponse est un paramètre privé de son nom et de son emplacement : le nom est la clé
    /// qui le porte, et l'emplacement va de soi. Décrire les champs une fois évite que les deux tables
    /// divergent, ce qui se verrait comme une différence signalée d'un côté et ignorée de l'autre.
    /// </remarks>
    private static IEnumerable<(string Field, FieldDescriptor Descriptor)> SharedParameterFields()
    {
        yield return ("description", FieldDescriptor.Documentation);

        // Rendre un paramètre obligatoire casse l'appelant qui l'omettait ; le rendre facultatif ne casse
        // personne. C'est le sens du drapeau qui décide, non le fait qu'il ait changé.
        yield return ("required", new FieldDescriptor(
            ValueRule: ValueAwareRule.RequirednessFlag,
            WhenAdded: ChangeImpact.Unknown,
            WhenRemoved: ChangeImpact.NonBreaking,
            WhenModified: ChangeImpact.Unknown));

        yield return ("deprecated", FieldDescriptor.Documentation);
        yield return ("allowEmptyValue", FieldDescriptor.Contractual);
        yield return ("style", FieldDescriptor.Contractual);
        yield return ("explode", FieldDescriptor.Contractual);
        yield return ("allowReserved", FieldDescriptor.Contractual);

        yield return ("schema", new FieldDescriptor(
            Child: SpecObjectKind.Schema,
            WhenModified: ChangeImpact.Unknown));

        yield return ("content", new FieldDescriptor(
            Child: SpecObjectKind.MediaType,
            Collection: CollectionKind.KeyedMap,
            WhenModified: ChangeImpact.Unknown));

        yield return ("example", FieldDescriptor.Documentation);

        yield return ("examples", new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.Example,
            Collection: CollectionKind.KeyedMap,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly));
    }

    private static void Headers(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        foreach ((string field, FieldDescriptor descriptor) in SharedParameterFields())
        {
            rows[(SpecObjectKind.Header, field)] = descriptor;
        }
    }

    private static void Bodies(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.RequestBody, "description")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.RequestBody, "required")] = new FieldDescriptor(
            ValueRule: ValueAwareRule.RequirednessFlag,
            WhenAdded: ChangeImpact.Unknown,
            WhenRemoved: ChangeImpact.NonBreaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.RequestBody, "content")] = new FieldDescriptor(
            Child: SpecObjectKind.MediaType,
            Collection: CollectionKind.KeyedMap,
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.MediaType, "schema")] = new FieldDescriptor(
            Child: SpecObjectKind.Schema,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.MediaType, "example")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.MediaType, "examples")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.Example,
            Collection: CollectionKind.KeyedMap,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.MediaType, "encoding")] = new FieldDescriptor(
            Child: SpecObjectKind.Encoding,
            Collection: CollectionKind.KeyedMap,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Encoding, "contentType")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.Encoding, "style")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.Encoding, "explode")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.Encoding, "allowReserved")] = FieldDescriptor.Contractual;

        rows[(SpecObjectKind.Encoding, "headers")] = new FieldDescriptor(
            Child: SpecObjectKind.Header,
            Collection: CollectionKind.KeyedMap,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Example, "summary")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Example, "description")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Example, "value")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Example, "externalValue")] = FieldDescriptor.Documentation;
    }

    private static void Responses(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        // Une réponse sans description est invalide, mais sa description reste de la documentation.
        rows[(SpecObjectKind.Response, "description")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.Response, "headers")] = new FieldDescriptor(
            Child: SpecObjectKind.Header,
            Collection: CollectionKind.KeyedMap,
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Response, "content")] = new FieldDescriptor(
            Child: SpecObjectKind.MediaType,
            Collection: CollectionKind.KeyedMap,
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Response, "links")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.Link,
            Collection: CollectionKind.KeyedMap,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);
    }

    private static void Links(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        foreach (string field in new[]
        {
            "operationRef", "operationId", "parameters", "requestBody", "description", "server",
        })
        {
            rows[(SpecObjectKind.Link, field)] = FieldDescriptor.Documentation;
        }
    }
}
