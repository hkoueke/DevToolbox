using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>
/// Les lectures d'arbre dont toutes les règles ont besoin.
/// </summary>
/// <remarks>
/// Trente-cinq règles qui réécriraient chacune « la valeur de cette clé, si c'est un scalaire » finiraient
/// par ne pas la lire tout à fait de la même façon.
/// </remarks>
internal static class Nodes
{
    /// <summary>La valeur textuelle d'une clé, telle qu'elle était écrite.</summary>
    internal static string? Text(SpecNode? node, string key) =>
        (node as MappingNode)?.Find(key) is ScalarNode scalar ? scalar.Text : null;

    /// <summary>Le scalaire porté par une clé.</summary>
    internal static ScalarNode? Scalar(SpecNode? node, string key) =>
        (node as MappingNode)?.Find(key) as ScalarNode;

    /// <summary>La valeur booléenne d'une clé, quand elle en est une.</summary>
    internal static bool? Flag(SpecNode? node, string key) =>
        Scalar(node, key) is { Resolved.Kind: ScalarValueKind.Boolean } scalar
            ? string.Equals(scalar.Resolved.Canonical, "true", StringComparison.Ordinal)
            : null;

    /// <summary>L'association portée par une clé.</summary>
    internal static MappingNode? Map(SpecNode? node, string key) =>
        (node as MappingNode)?.Find(key) as MappingNode;

    /// <summary>La suite portée par une clé.</summary>
    internal static SequenceNode? Sequence(SpecNode? node, string key) =>
        (node as MappingNode)?.Find(key) as SequenceNode;

    /// <summary>Les éléments de la suite portée par une clé, ou rien.</summary>
    internal static IReadOnlyList<SpecNode> Items(SpecNode? node, string key) =>
        Sequence(node, key)?.Items ?? [];

    /// <summary>Indique si une clé est présente, quelle que soit sa valeur.</summary>
    internal static bool Has(SpecNode? node, string key) =>
        (node as MappingNode)?.Find(key) is not null;

    /// <summary>
    /// Suit une référence interne, et rend le nœud désigné.
    /// </summary>
    /// <remarks>
    /// Un nœud qui n'est qu'une référence ne dit rien de lui-même : une règle qui l'examinerait tel quel
    /// conclurait qu'un paramètre n'a pas de nom. Les références externes ne sont pas suivies, et c'est
    /// pourquoi cette fonction peut rendre <see langword="null"/> : la règle doit alors avouer qu'elle n'a
    /// pas regardé, et non décider.
    /// </remarks>
    internal static SpecNode? Resolve(SpecNode root, SpecNode node)
    {
        if (Text(node, "$ref") is not { } reference)
        {
            return node;
        }

        ReferenceTarget target = ReferenceTarget.Parse(reference);

        return target.IsExternal ? null : PointerNavigator.Find(root, target.Fragment);
    }

    /// <summary>Le dernier segment d'un emplacement : le nom sous lequel la chose est rangée.</summary>
    internal static string Name(SpecPointer location) =>
        location.Segments.Count == 0 ? string.Empty : location.Segments[^1];

    /// <summary>Indique si l'emplacement est celui d'un composant nommé d'une famille donnée.</summary>
    internal static bool IsComponent(SpecPointer location, string family) =>
        location.Segments.Count == 3
        && string.Equals(location.Segments[0], "components", StringComparison.Ordinal)
        && string.Equals(location.Segments[1], family, StringComparison.Ordinal);

    /// <summary>Indique si l'emplacement est quelque part sous <c>components</c>.</summary>
    internal static bool IsUnderComponents(SpecPointer location) =>
        location.Segments.Count > 0
        && string.Equals(location.Segments[0], "components", StringComparison.Ordinal);

    /// <summary>Indique si le nœud n'est qu'une référence à autre chose.</summary>
    internal static bool IsReference(SpecNode node) => Has(node, "$ref");
}
