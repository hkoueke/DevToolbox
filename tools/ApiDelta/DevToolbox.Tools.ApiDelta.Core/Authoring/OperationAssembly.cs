using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Assemble l'objet d'une opération à partir des placements que le développeur a arrêtés.</summary>
/// <remarks>
/// La projection produit une opération dont l'entrée est un corps, parce que c'est tout ce qu'un contrat
/// SOAP décrit. Composer consiste à redistribuer cette entrée là où le développeur l'a placée — et c'est
/// ici que cela se fait, en reprenant de l'opération projetée tout ce qui ne concerne pas l'entrée.
/// <para>
/// Ce qui n'est pas touché est repris tel quel : les réponses, les étiquettes, le résumé. Cette
/// fonctionnalité décide où l'entrée voyage, pas ce que l'opération dit par ailleurs.
/// </para>
/// </remarks>
public static class OperationAssembly
{
    private const string Parameters = "parameters";
    private const string RequestBody = "requestBody";
    private const string Content = "content";
    private const string Json = "application/json";
    private const string Schema = "schema";
    private const string Required = "required";
    private const string Name = "name";
    private const string In = "in";

    /// <summary>Assemble l'opération composée.</summary>
    /// <param name="projected">L'opération telle que la projection l'a produite.</param>
    /// <param name="route">Ce que le développeur a arrêté.</param>
    /// <param name="headers">Les références d'en-tête acceptées, déjà résolues en composants.</param>
    /// <param name="document">La projection complète pour résoudre les références de schémas.</param>
    /// <returns>L'objet à écrire.</returns>
    /// <remarks>
    /// Sans décision sur les champs ni ajout d'en-tête, l'opération reste inchangée. Dès qu'un placement
    /// est arrêté, ses champs et son caractère obligatoire déterminent l'entrée reconstruite.
    /// </remarks>
    public static SpecNode Compose(
        SpecNode projected, AuthoredRoute route, IReadOnlyList<string> headers, SpecNode? document = null)
    {
        ArgumentNullException.ThrowIfNull(projected);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(headers);

        if (projected is not MappingNode operation)
        {
            return projected;
        }

        if (route.Placements.Count == 0 && headers.Count == 0)
        {
            return projected;
        }

        // L'entrée est reconstruite ; tout le reste de l'opération est repris tel quel.
        List<MappingEntry> entries =
        [
            .. operation.Entries.Where(entry => !IsInput(entry.Key.Text)),
        ];

        ProjectedInput input = new(operation, document ?? projected);
        SpecNode? listed = Listed(route, headers, input);

        if (listed is not null)
        {
            entries.Add(new MappingEntry(TreeEdits.Key(Parameters), listed));
        }

        if (Body(route, input) is { } body)
        {
            entries.Add(new MappingEntry(TreeEdits.Key(RequestBody), body));
        }

        return TreeEdits.Mapping(operation.Location, entries);
    }

    /// <summary>Indique qu'un membre de l'opération décrit son entrée.</summary>
    private static bool IsInput(string key) =>
        string.Equals(key, Parameters, StringComparison.Ordinal)
        || string.Equals(key, RequestBody, StringComparison.Ordinal);

    /// <summary>Les paramètres : les champs placés hors du corps, puis les en-têtes repris.</summary>
    /// <remarks>
    /// Les en-têtes viennent après les champs, et par référence : ce sont des composants que la
    /// production déclare déjà, et les redéclarer ferait deux définitions d'une même chose.
    /// </remarks>
    private static SequenceNode? Listed(AuthoredRoute route, IReadOnlyList<string> headers, ProjectedInput input)
    {
        List<SpecNode> items = [];

        foreach (FieldPlacement placement in route.AsParameters)
        {
            items.Add(Parameter(placement, input));
        }

        foreach (string component in headers)
        {
            items.Add(TreeEdits.Reference("#/components/parameters/" + component));
        }

        return items.Count == 0 ? null : TreeEdits.Sequence(SpecPointer.Root, items);
    }

    /// <summary>Un paramètre, tel qu'OpenAPI l'écrit.</summary>
    /// <remarks>
    /// Le schéma est repris du champ : un scalaire, puisque seul un scalaire peut être un paramètre. Le
    /// type déclaré du contrat n'est pas inscrit tel quel — « xs:string » n'est pas un type OpenAPI.
    /// </remarks>
    private static MappingNode Parameter(FieldPlacement placement, ProjectedInput input) => TreeEdits.Mapping(
        SpecPointer.Root,
        [
            new MappingEntry(TreeEdits.Key(Name), TreeEdits.Key(placement.Effective)),
            new MappingEntry(TreeEdits.Key(In), TreeEdits.Key(Wire(placement.Location))),
            new MappingEntry(TreeEdits.Key(Required), TreeEdits.Flag(placement.Location is ParameterLocation.Path || placement.Required)),
            new MappingEntry(
                TreeEdits.Key(Schema),
                input.Field(placement)),
        ]);

    /// <summary>Le corps limité aux champs retenus, ou rien si tous sont placés ailleurs ou écartés.</summary>
    /// <remarks>
    /// Le schéma de chaque propriété vient de la projection ; le composant d'entrée partagé reste intact.
    /// </remarks>
    private static MappingNode? Body(AuthoredRoute route, ProjectedInput input)
    {
        if (route.Placements.Count == 0)
        {
            return input.RequestBody;
        }

        FieldPlacement[] fields = [.. route.InBody];
        SpecNode schema = input.BodySchema(fields);
        if (fields.Length == 0)
        {
            return null;
        }

        List<MappingEntry> media = input.Content is { } content
            ? [.. content.Entries.Select(entry => new MappingEntry(entry.Key,
                TreeEdits.Mapping(SpecPointer.Root,
                    [.. (entry.Value as MappingNode)?.Entries.Where(value => !string.Equals(value.Key.Text, Schema, StringComparison.Ordinal)) ?? [],
                    new MappingEntry(TreeEdits.Key(Schema), schema)])))]
            : [new MappingEntry(TreeEdits.Key(Json), TreeEdits.Mapping(SpecPointer.Root, [new MappingEntry(TreeEdits.Key(Schema), schema)]))];
        return TreeEdits.Mapping(SpecPointer.Root,
        [
            .. input.RequestBody?.Entries.Where(entry => !string.Equals(entry.Key.Text, Required, StringComparison.Ordinal)
                && !string.Equals(entry.Key.Text, Content, StringComparison.Ordinal)) ?? [],
            new MappingEntry(TreeEdits.Key(Required), TreeEdits.Flag(fields.Any(field => field.Required))),
            new MappingEntry(TreeEdits.Key(Content), TreeEdits.Mapping(SpecPointer.Root, media)),
        ]);
    }

    /// <summary>Le nom de la destination, tel qu'OpenAPI l'écrit.</summary>
    private static string Wire(ParameterLocation location) => location switch
    {
        ParameterLocation.Path => "path",
        ParameterLocation.Query => "query",
        ParameterLocation.Header => "header",
        _ => throw new ArgumentOutOfRangeException(
            nameof(location), location, "Cette destination n'est pas un paramètre."),
    };
}
