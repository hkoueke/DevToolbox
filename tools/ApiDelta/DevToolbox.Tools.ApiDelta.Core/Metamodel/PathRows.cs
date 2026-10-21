using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Les champs d'un élément de chemin et d'une opération.
/// </summary>
/// <remarks>
/// Les huit méthodes sont nommées explicitement : dans un élément de chemin, ce qui n'est pas une méthode
/// est un champ — <c>parameters</c>, <c>summary</c>, <c>servers</c> — et les confondre produirait des
/// opérations fantômes qu'aucune comparaison ne saurait démêler.
/// </remarks>
internal static class PathRows
{
    /// <summary>Les méthodes qu'un élément de chemin peut porter.</summary>
    internal static IReadOnlySet<string> Methods { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "get", "put", "post", "delete", "options", "head", "patch", "trace",
    };

    internal static void Register(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        PathItem(rows);
        Operation(rows);
    }

    private static void PathItem(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        foreach (string method in Methods)
        {
            rows[(SpecObjectKind.PathItem, method)] = new FieldDescriptor(
                Child: SpecObjectKind.Operation,
                WhenAdded: ChangeImpact.NonBreaking,
                WhenRemoved: ChangeImpact.Breaking,
                WhenModified: ChangeImpact.Unknown);
        }

        rows[(SpecObjectKind.PathItem, "summary")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.PathItem, "description")] = FieldDescriptor.Documentation;

        // Les paramètres d'un élément de chemin valent pour toutes ses opérations. En retirer un retire
        // donc une obligation à chacune, et en ajouter un en ajoute une à chacune.
        rows[(SpecObjectKind.PathItem, "parameters")] = ParameterList();

        rows[(SpecObjectKind.PathItem, "servers")] = new FieldDescriptor(
            Child: SpecObjectKind.Server,
            Collection: CollectionKind.Ordered,
            Identity: IdentityRule.ByIndex,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Breaking);
    }

    private static void Operation(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Operation, "summary")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Operation, "description")] = FieldDescriptor.Documentation;

        // L'identifiant d'opération n'est pas de la documentation : les clients engendrés en tirent le nom
        // de leur méthode, si bien qu'en changer casse le code de l'appelant à la compilation.
        rows[(SpecObjectKind.Operation, "operationId")] = new FieldDescriptor(
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Operation, "tags")] = new FieldDescriptor(
            FieldRole.Documentation,
            Collection: CollectionKind.SetLike,
            Identity: IdentityRule.ByValue,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Operation, "externalDocs")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.ExternalDocs,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Operation, "parameters")] = ParameterList();

        rows[(SpecObjectKind.Operation, "requestBody")] = new FieldDescriptor(
            Child: SpecObjectKind.RequestBody,
            WhenAdded: ChangeImpact.Unknown,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Operation, "responses")] = new FieldDescriptor(
            Child: SpecObjectKind.Response,
            Collection: CollectionKind.KeyedMap,

            // Une réponse ajoutée relève de la politique : elle n'empêche personne d'appeler, mais un
            // client engendré qui traite exhaustivement les codes peut cesser de compiler.
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            Policy: VersioningPolicyKeys.ResponseAdded);

        rows[(SpecObjectKind.Operation, "callbacks")] = new FieldDescriptor(
            Child: SpecObjectKind.Callback,
            Collection: CollectionKind.KeyedMap,
            WhenModified: ChangeImpact.Unknown);

        Security(rows);
    }

    private static void Security(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Operation, "deprecated")] = new FieldDescriptor(
            FieldRole.Documentation,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Operation, "security")] = new FieldDescriptor(
            Child: SpecObjectKind.SecurityRequirement,
            Collection: CollectionKind.Ordered,
            Identity: IdentityRule.ByIndex,
            ValueRule: ValueAwareRule.SecurityRequirement,
            WhenAdded: ChangeImpact.Breaking,

            // Retirer le bloc d'une opération la ramène à la sécurité globale, ce qui peut la durcir
            // comme l'alléger : l'outil ne peut pas le dire sans regarder le document entier.
            WhenRemoved: ChangeImpact.Unknown,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Operation, "servers")] = new FieldDescriptor(
            Child: SpecObjectKind.Server,
            Collection: CollectionKind.Ordered,
            Identity: IdentityRule.ByIndex,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Breaking);
    }

    /// <summary>
    /// Une liste de paramètres : un ensemble dont les membres se reconnaissent par nom et emplacement.
    /// </summary>
    /// <remarks>
    /// L'identité est le couple, et non le rang : réordonner des paramètres ne les remplace pas, et c'est
    /// la différence entre « ce paramètre est devenu obligatoire » et « un paramètre a disparu, un autre
    /// est apparu ».
    /// </remarks>
    private static FieldDescriptor ParameterList() => new(
        Child: SpecObjectKind.Parameter,
        Collection: CollectionKind.SetLike,
        Identity: IdentityRule.ByNameAndLocation,
        WhenAdded: ChangeImpact.Unknown,
        WhenRemoved: ChangeImpact.NonBreaking,
        WhenModified: ChangeImpact.Unknown);
}
