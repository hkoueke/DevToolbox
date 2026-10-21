using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Ce que l'outil sait de la grammaire d'OpenAPI, sous forme de table.
/// </summary>
/// <remarks>
/// Une table, et non la même connaissance répartie entre un comparateur, trois canonisateurs et cinquante
/// règles : ainsi se relit-elle ligne à ligne, et une ligne qu'un relecteur peut contredire est une ligne
/// qui vaut quelque chose.
/// <para>
/// La résolution descend l'arbre en suivant les sortes d'objets plutôt qu'en appariant des motifs de
/// chemin, parce qu'en OpenAPI c'est la position qui décide de la nature : un schéma peut s'imbriquer
/// indéfiniment en lui-même, et aucun motif ne dirait où il commence.
/// </para>
/// </remarks>
public sealed class OpenApiMetamodel : IMetamodel
{
    private readonly Dictionary<(SpecObjectKind, string), FieldDescriptor> _rows;

    /// <summary>Construit la table.</summary>
    public OpenApiMetamodel()
    {
        _rows = new Dictionary<(SpecObjectKind, string), FieldDescriptor>();

        DocumentRows.Register(_rows);
        PathRows.Register(_rows);
        OperationContentRows.Register(_rows);
        SchemaRows.Register(_rows);
        ComponentRows.Register(_rows);
    }

    /// <inheritdoc />
    public MetamodelEntry Resolve(SpecPointer location, OpenApiVersion version)
    {
        ArgumentNullException.ThrowIfNull(location);

        IReadOnlyList<string> segments = location.Segments;

        if (segments.Count == 0)
        {
            return new MetamodelEntry(
                SpecObjectKind.Document,
                FieldRole.Contract,
                ChildKind: SpecObjectKind.Document);
        }

        SpecObjectKind kind = SpecObjectKind.Document;
        MetamodelEntry? entry = null;

        // Le seul état qu'il faut tenir : la collection dont on attend encore la clé. Sans lui, un champ
        // qui suit une clé de collection — « required » après « Document » sous « schemas » — se prendrait
        // pour un membre de cette collection, et hériterait d'une identité par clé au lieu de la sienne.
        MetamodelEntry? awaitingKey = null;

        foreach (string segment in segments)
        {
            if (segment.StartsWith("x-", StringComparison.Ordinal))
            {
                entry = Vendor(kind);
                kind = SpecObjectKind.Extension;
                awaitingKey = null;
                continue;
            }

            if (awaitingKey is not null)
            {
                // Ce segment est une clé ou un rang : il désigne un membre, dont la sorte a déjà été
                // annoncée par le champ qui porte la collection.
                entry = Member(awaitingKey, kind);
                awaitingKey = null;
                continue;
            }

            if (!_rows.TryGetValue((kind, segment), out FieldDescriptor? descriptor))
            {
                return MetamodelEntry.SafeDefault;
            }

            entry = Describe(kind, descriptor);
            kind = entry.ChildKind;
            awaitingKey = descriptor.Collection == CollectionKind.None ? null : entry;
        }

        return entry ?? MetamodelEntry.SafeDefault;
    }

    /// <summary>
    /// Un membre de collection : il hérite de l'effet déclaré par le champ qui le porte.
    /// </summary>
    /// <remarks>
    /// Une réponse ajoutée vaut ce que <c>responses</c> dit d'une addition, et le membre n'a pas à le
    /// redire — ce qui évite d'avoir à décrire chaque code de statut.
    /// </remarks>
    private static MetamodelEntry Member(MetamodelEntry collection, SpecObjectKind kind) => new(
        kind,
        collection.Role,
        CollectionKind.None,
        collection.Identity,
        collection.WhenAdded,
        collection.WhenRemoved,
        collection.WhenModified,
        collection.ValueRule,
        collection.Policy,
        kind);

    private static MetamodelEntry Vendor(SpecObjectKind kind) => new(
        kind,
        FieldRole.Vendor,
        WhenAdded: Comparison.ChangeImpact.Unknown,
        WhenRemoved: Comparison.ChangeImpact.Unknown,
        WhenModified: Comparison.ChangeImpact.Unknown,
        ChildKind: SpecObjectKind.Extension);

    private static MetamodelEntry Describe(SpecObjectKind kind, FieldDescriptor descriptor) => new(
        kind,
        descriptor.Role,
        descriptor.Collection,
        descriptor.Identity,
        descriptor.WhenAdded,
        descriptor.WhenRemoved,
        descriptor.WhenModified,
        descriptor.ValueRule,
        descriptor.Policy,
        descriptor.Child);
}
