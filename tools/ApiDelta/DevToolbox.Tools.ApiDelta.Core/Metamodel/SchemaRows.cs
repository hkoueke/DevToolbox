using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Les mots-clés d'un schéma, pour les deux versions prises en charge.
/// </summary>
/// <remarks>
/// Un schéma s'imbrique en lui-même sans limite — par ses propriétés, ses éléments, ses compositions — et
/// c'est pourquoi le métamodèle descend en suivant des sortes plutôt qu'en appariant des motifs de chemin.
/// <para>
/// Le sens du resserrement est ce qui compte ici : abaisser <c>maxLength</c> refuse ce qui passait,
/// l'augmenter ne refuse rien. Chaque borne porte donc sa règle, et le classificateur regarde la valeur.
/// </para>
/// </remarks>
internal static class SchemaRows
{
    internal static void Register(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        Identity(rows);
        Constraints(rows);
        Structure(rows);
        Composition(rows);
    }

    private static void Identity(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Schema, "type")] = new FieldDescriptor(
            ValueRule: ValueAwareRule.TypeName,

            // En 3.1, « type » peut être une liste : ajouter « null » élargit, la retirer resserre. La
            // règle sensible à la valeur traite les deux formes.
            Collection: CollectionKind.SetLike,
            Identity: IdentityRule.ByValue,
            WhenAdded: ChangeImpact.Breaking,
            WhenRemoved: ChangeImpact.Unknown,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Schema, "format")] = new FieldDescriptor(
            ValueRule: ValueAwareRule.FormatName,
            WhenAdded: ChangeImpact.Unknown,
            WhenRemoved: ChangeImpact.Unknown,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Schema, "title")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Schema, "description")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Schema, "deprecated")] = FieldDescriptor.Documentation;
        rows[(SpecObjectKind.Schema, "example")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.Schema, "examples")] = new FieldDescriptor(
            FieldRole.Documentation,
            Collection: CollectionKind.Ordered,
            Identity: IdentityRule.ByIndex,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Schema, "externalDocs")] = new FieldDescriptor(
            FieldRole.Documentation,
            Child: SpecObjectKind.ExternalDocs,
            WhenAdded: ChangeImpact.DocumentationOnly,
            WhenRemoved: ChangeImpact.DocumentationOnly,
            WhenModified: ChangeImpact.DocumentationOnly);

        rows[(SpecObjectKind.Schema, "$ref")] = new FieldDescriptor(
            WhenAdded: ChangeImpact.Unknown,
            WhenRemoved: ChangeImpact.Unknown,
            WhenModified: ChangeImpact.Unknown);

        // La nullabilité de 3.0. En 3.1, elle s'exprime par une union avec « null » dans « type », et la
        // comparaison doit lire les deux formes comme la même intention.
        rows[(SpecObjectKind.Schema, "nullable")] = new FieldDescriptor(
            ValueRule: ValueAwareRule.PermissionFlag,
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Schema, "readOnly")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.Schema, "writeOnly")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.Schema, "default")] = FieldDescriptor.Contractual;
        rows[(SpecObjectKind.Schema, "const")] = FieldDescriptor.Contractual;
    }

    private static void Constraints(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        // Une énumération qui se raccourcit refuse ce qui passait ; qui s'allonge, cela dépend de ce que
        // le client engendré fait d'une valeur qu'il ne connaît pas — donc d'une politique.
        rows[(SpecObjectKind.Schema, "enum")] = new FieldDescriptor(
            Collection: CollectionKind.SetLike,
            Identity: IdentityRule.ByValue,
            ValueRule: ValueAwareRule.EnumMembers,
            WhenAdded: ChangeImpact.Breaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Unknown,
            Policy: VersioningPolicyKeys.EnumValueAdded);

        foreach (string upper in new[]
        {
            "maximum", "exclusiveMaximum", "maxLength", "maxItems", "maxProperties", "multipleOf",
        })
        {
            rows[(SpecObjectKind.Schema, upper)] = new FieldDescriptor(
                ValueRule: ValueAwareRule.UpperBound,
                WhenAdded: ChangeImpact.Breaking,
                WhenRemoved: ChangeImpact.NonBreaking,
                WhenModified: ChangeImpact.Unknown);
        }

        foreach (string lower in new[]
        {
            "minimum", "exclusiveMinimum", "minLength", "minItems", "minProperties",
        })
        {
            rows[(SpecObjectKind.Schema, lower)] = new FieldDescriptor(
                ValueRule: ValueAwareRule.LowerBound,
                WhenAdded: ChangeImpact.Breaking,
                WhenRemoved: ChangeImpact.NonBreaking,
                WhenModified: ChangeImpact.Unknown);
        }

        rows[(SpecObjectKind.Schema, "pattern")] = new FieldDescriptor(
            WhenAdded: ChangeImpact.Breaking,
            WhenRemoved: ChangeImpact.NonBreaking,

            // Deux expressions peuvent décrire le même langage, et comparer des langages réguliers n'est
            // pas le métier de cet outil : le changement est signalé comme inconnu.
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Schema, "uniqueItems")] = new FieldDescriptor(
            ValueRule: ValueAwareRule.RequirednessFlag,
            WhenAdded: ChangeImpact.Breaking,
            WhenRemoved: ChangeImpact.NonBreaking,
            WhenModified: ChangeImpact.Unknown);
    }

    private static void Structure(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        rows[(SpecObjectKind.Schema, "properties")] = new FieldDescriptor(
            Child: SpecObjectKind.Schema,
            Collection: CollectionKind.KeyedMap,

            // Une propriété ajoutée est compatible tant qu'elle n'est pas obligatoire ; c'est « required »
            // qui porte l'obligation, et c'est lui qui rompt.
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Schema, "required")] = new FieldDescriptor(
            Collection: CollectionKind.SetLike,
            Identity: IdentityRule.ByValue,
            ValueRule: ValueAwareRule.RequiredNames,
            WhenAdded: ChangeImpact.Breaking,
            WhenRemoved: ChangeImpact.NonBreaking,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Schema, "items")] = new FieldDescriptor(
            Child: SpecObjectKind.Schema,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Schema, "prefixItems")] = new FieldDescriptor(
            Child: SpecObjectKind.Schema,
            Collection: CollectionKind.Ordered,
            Identity: IdentityRule.ByIndex,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Schema, "additionalProperties")] = new FieldDescriptor(
            Child: SpecObjectKind.Schema,
            WhenAdded: ChangeImpact.Unknown,
            WhenRemoved: ChangeImpact.Unknown,
            WhenModified: ChangeImpact.Unknown);

        rows[(SpecObjectKind.Schema, "discriminator")] = new FieldDescriptor(
            Child: SpecObjectKind.Discriminator,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Discriminator, "propertyName")] = FieldDescriptor.Contractual;

        rows[(SpecObjectKind.Discriminator, "mapping")] = new FieldDescriptor(
            Collection: CollectionKind.KeyedMap,
            WhenModified: ChangeImpact.Breaking);

        rows[(SpecObjectKind.Schema, "xml")] = new FieldDescriptor(
            Child: SpecObjectKind.Xml,
            WhenModified: ChangeImpact.Breaking);

        foreach (string field in new[] { "name", "namespace", "prefix", "attribute", "wrapped" })
        {
            rows[(SpecObjectKind.Xml, field)] = FieldDescriptor.Contractual;
        }
    }

    private static void Composition(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        // L'ordre d'une composition est conservé : des outils en dépendent pour nommer les types
        // engendrés, et réordonner peut donc changer ce que l'appelant compile.
        foreach (string field in new[] { "allOf", "anyOf", "oneOf" })
        {
            rows[(SpecObjectKind.Schema, field)] = new FieldDescriptor(
                Child: SpecObjectKind.Schema,
                Collection: CollectionKind.Ordered,
                Identity: IdentityRule.ByIndex,
                WhenAdded: ChangeImpact.Unknown,
                WhenRemoved: ChangeImpact.Unknown,
                WhenModified: ChangeImpact.Unknown);
        }

        foreach (string field in new[] { "not", "if", "then", "else" })
        {
            rows[(SpecObjectKind.Schema, field)] = new FieldDescriptor(
                Child: SpecObjectKind.Schema,
                WhenAdded: ChangeImpact.Unknown,
                WhenRemoved: ChangeImpact.Unknown,
                WhenModified: ChangeImpact.Unknown);
        }
    }
}
