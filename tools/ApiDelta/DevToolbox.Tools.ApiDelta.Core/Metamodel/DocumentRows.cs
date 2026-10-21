using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Les champs du document et de ses blocs de tête : <c>info</c>, <c>servers</c>, <c>tags</c>,
/// <c>externalDocs</c>, <c>security</c>.
/// </summary>
/// <remarks>
/// Le champ <c>info.version</c> mérite son commentaire : il est l'entrée du moteur de version, et il ne
/// doit surtout pas masquer les autres changements d'<c>info</c>. Il est donc décrit comme n'importe quel
/// autre champ, et c'est le moteur de version — non la comparaison — qui le traite à part.
/// </remarks>
internal static class DocumentRows
{
    internal static void Register(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        Document(rows);
        Info(rows);
        Servers(rows);
        Tags(rows);
    }

    private static void Document(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Document, "openapi")] = new FieldDescriptor(
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Document, "info")] = new FieldDescriptor(Child: SpecObjectKind.Info);

        rows[(SpecObjectKind.Document, "servers")] = new FieldDescriptor(
            Child: SpecObjectKind.Server,
            Collection: CollectionKind.Ordered,
            Identity: IdentityRule.ByIndex,
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Document, "paths")] = new FieldDescriptor(
            Child: SpecObjectKind.PathItem,
            Collection: CollectionKind.KeyedMap);

        rows[(SpecObjectKind.Document, "webhooks")] = new FieldDescriptor(
            Child: SpecObjectKind.PathItem,
            Collection: CollectionKind.KeyedMap);

        rows[(SpecObjectKind.Document, "components")] = new FieldDescriptor(
            Child: SpecObjectKind.Components,
            // Un composant n'est un contrat que par ses usages : ce qui compte est ce qui le référence.
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Unknown,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Document, "security")] = new FieldDescriptor(
            Child: SpecObjectKind.SecurityRequirement,

            // L'ordre des alternatives peut compter pour les outils qui prennent la première : il est
            // conservé, et une réorganisation reste un changement inconnu plutôt qu'un non-changement.
            Collection: CollectionKind.Ordered,
            Identity: IdentityRule.ByIndex,
            ValueRule: ValueAwareRule.SecurityRequirement,
            WhenAdded: ChangeImpact.Breaking,
            WhenRemoved: ChangeImpact.NonBreaking,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Document, "tags")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.Tag,
            Collection: CollectionKind.SetLike,
            Identity: IdentityRule.ByValue,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Document, "externalDocs")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.ExternalDocs,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Document, "jsonSchemaDialect")] = FieldDescriptor.Contractual;
    }

    private static void Info(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Info, "title")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Info, "summary")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Info, "description")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Info, "termsOfService")] = FieldDescriptor.Documentation;

        // La version est lue par le moteur de version. Décrite ici comme documentation pour que son
        // changement ne compte pas comme un changement d'API — publier une version est l'effet, non la
        // cause.
        rows[(SpecObjectKind.Info, "version")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.Info, "contact")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.Contact,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Info, "license")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.License,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        foreach (string field in new[] { "name", "url", "email" })
        {
            rows[(SpecObjectKind.Contact, field)] = FieldDescriptor.Documentation;
        }

        rows[(SpecObjectKind.License, "name")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.License, "identifier")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.License, "url")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.ExternalDocs, "description")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.ExternalDocs, "url")] = FieldDescriptor.Documentation;
    }

    private static void Servers(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Server, "url")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.Server, "description")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.Server, "variables")] = new FieldDescriptor(
            Child: SpecObjectKind.ServerVariable,
            Collection: CollectionKind.KeyedMap,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.ServerVariable, "default")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.ServerVariable, "description")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.ServerVariable, "enum")] = new FieldDescriptor(
            Collection: CollectionKind.SetLike,
            Identity: IdentityRule.ByValue,
            ValueRule: ValueAwareRule.EnumMembers,
            WhenModified: ChangeImpact.Unknown);
    }

    private static void Tags(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Tag, "name")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Tag, "description")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.Tag, "externalDocs")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.ExternalDocs,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);
    }
}
