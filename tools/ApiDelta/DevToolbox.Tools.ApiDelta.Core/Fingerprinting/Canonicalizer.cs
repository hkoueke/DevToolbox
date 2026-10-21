using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>
/// Réduit un sous-arbre à ce qu'il dit, en écartant la manière dont il est écrit.
/// </summary>
/// <remarks>
/// C'est ici que la mise en forme devient invisible : position, style, indentation, ordre des clés,
/// quotation et rangement d'un ensemble disparaissent. Ce qui reste est ce qu'un appelant peut constater,
/// et c'est pourquoi une description réécrite autrement produit la même forme.
/// <para>
/// Ce qui est <em>conservé</em> compte autant. L'ordre d'une suite dont l'ordre a un sens, chaque mot-clé
/// de validation et chaque extension de fournisseur restent, parce que normaliser plus loin reviendrait à
/// décider que deux contrats différents sont le même. Les nombres, eux, sont ramenés à une seule
/// écriture : <c>1.0</c> et <c>1</c> sont une seule contrainte, et l'arbre se souvient encore des deux
/// orthographes pour qui veut signaler la différence de forme.
/// </para>
/// </remarks>
public sealed class Canonicalizer : ICanonicalizer
{
    private readonly IMetamodel _metamodel;

    /// <summary>Crée la canonisation.</summary>
    /// <param name="metamodel">Dit ce qu'est un emplacement : un ensemble, une suite, de la documentation.</param>
    public Canonicalizer(IMetamodel metamodel)
    {
        ArgumentNullException.ThrowIfNull(metamodel);
        _metamodel = metamodel;
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Canonicalize(
        SpecNode node,
        EquivalenceProfile profile,
        OpenApiVersion version) =>
        Encoding.UTF8.GetBytes(Text(node, profile, version));

    /// <inheritdoc />
    public string Text(SpecNode node, EquivalenceProfile profile, OpenApiVersion version)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(profile);

        return Write(node, _metamodel, version, profile);
    }

    /// <summary>Canonise un sous-arbre sans passer par une instance.</summary>
    /// <param name="node">Le nœud.</param>
    /// <param name="metamodel">La table.</param>
    /// <param name="version">La version déclarée.</param>
    /// <param name="profile">Le profil.</param>
    /// <returns>La forme canonique.</returns>
    internal static string Write(
        SpecNode node,
        IMetamodel metamodel,
        OpenApiVersion version,
        EquivalenceProfile profile)
    {
        StringBuilder builder = new();

        Append(builder, node, metamodel, version, profile);

        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder,
        SpecNode node,
        IMetamodel metamodel,
        OpenApiVersion version,
        EquivalenceProfile profile)
    {
        switch (node)
        {
            case ScalarNode scalar:
                // La valeur résolue, non le texte : « 200 » et « '200' » sont un seul code de réponse.
                builder.Append('"')
                    .Append(profile.ComparesValues ? scalar.Resolved.Canonical : Shape(scalar))
                    .Append('"');
                break;

            case MappingNode mapping:
                AppendMapping(builder, mapping, metamodel, version, profile);
                break;

            case SequenceNode sequence:
                AppendSequence(builder, sequence, metamodel, version, profile);
                break;
        }
    }

    /// <summary>La sorte d'une valeur, quand le profil ne compare que la structure.</summary>
    private static string Shape(ScalarNode scalar) =>
        scalar.Resolved.Kind.ToString().ToLowerInvariant();

    private static void AppendMapping(
        StringBuilder builder,
        MappingNode mapping,
        IMetamodel metamodel,
        OpenApiVersion version,
        EquivalenceProfile profile)
    {
        builder.Append('{');

        // Les clés sont triées par unité de code : l'ordre d'une association n'a aucun sens pour OpenAPI,
        // et deux documents qui ne diffèrent que par là ne diffèrent pas.
        foreach (MappingEntry entry in mapping.Entries
            .Where(entry => profile.Includes(metamodel.Resolve(entry.Value.Location, version)))
            .OrderBy(entry => entry.Key.Text, StringComparer.Ordinal))
        {
            builder.Append('"').Append(entry.Key.Text).Append("\":");
            Append(builder, entry.Value, metamodel, version, profile);
            builder.Append(',');
        }

        builder.Append('}');
    }

    private static void AppendSequence(
        StringBuilder builder,
        SequenceNode sequence,
        IMetamodel metamodel,
        OpenApiVersion version,
        EquivalenceProfile profile)
    {
        MetamodelEntry entry = metamodel.Resolve(sequence.Location, version);

        builder.Append('[');

        IEnumerable<SpecNode> items = sequence.Items;

        if (entry.Collection == CollectionKind.SetLike)
        {
            // Un ensemble : l'ordre d'écriture ne dit rien. Les membres sont donc triés par leur propre
            // forme canonique, ce qui rend la comparaison insensible à leur rangement.
            items = sequence.Items
                .Select(item => (Item: item, Key: Write(item, metamodel, version, profile)))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Item);
        }

        foreach (SpecNode item in items)
        {
            Append(builder, item, metamodel, version, profile);
            builder.Append(',');
        }

        builder.Append(']');
    }
}
