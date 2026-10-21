using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>
/// Une opération après application de son héritage : ses champs, plus ce que son contexte lui impose.
/// </summary>
/// <remarks>
/// C'est l'unité que la vérification compare, de part et d'autre, et c'est tout l'intérêt de ce type.
/// Comparer l'opération <em>écrite</em> laisserait passer un extrait qui aurait discrètement perdu un
/// paramètre déclaré au niveau du chemin ou l'exigence de sécurité globale — c'est-à-dire exactement la
/// panne que ce mode doit être incapable de produire.
/// <para>
/// Les règles de masquage sont celles d'OpenAPI : un paramètre de l'opération masque celui du chemin
/// lorsque le couple nom-emplacement est le même. Fusionner sans cette règle rendrait deux paramètres là
/// où le document n'en voit qu'un.
/// </para>
/// </remarks>
public static class EffectiveOperation
{
    private const string Parameters = "parameters";

    private const string Servers = "servers";

    private const string Security = "security";

    /// <summary>Assemble la forme effective d'une opération.</summary>
    /// <param name="document">La description qui la porte.</param>
    /// <param name="selector">L'opération.</param>
    /// <returns>La forme effective, ou <see langword="null"/> si l'opération n'est pas là.</returns>
    public static MappingNode? Of(SpecDocument document, OperationSelector selector)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(selector);

        if (Item(document, selector) is not { } item
            || item.Find(selector.MethodKey) is not MappingNode operation)
        {
            return null;
        }

        List<MappingEntry> entries =
        [
            .. operation.Entries.Where(entry => !Inherited(entry.Key.Text)),
        ];

        Add(entries, Parameters, Merged(operation, item, document.Root));
        Add(entries, Servers, InForce(operation, item, document.Root as MappingNode, Servers));
        Add(entries, Security, InForce(operation, item, document.Root as MappingNode, Security));

        return TreeEdits.Mapping(operation.Location, entries);
    }

    /// <summary>Les paramètres du niveau du chemin qu'une opération hérite réellement.</summary>
    /// <param name="document">La description.</param>
    /// <param name="selector">L'opération.</param>
    /// <returns>Les noms hérités, dans l'ordre du document.</returns>
    public static IReadOnlyList<string> InheritedParameters(
        SpecDocument document,
        OperationSelector selector)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(selector);

        if (Item(document, selector) is not { } item
            || item.Find(selector.MethodKey) is not MappingNode operation)
        {
            return [];
        }

        HashSet<string> own = new(Identities(operation, document.Root), StringComparer.Ordinal);

        return
        [
            .. Items(item, Parameters)
                .Where(parameter => !own.Contains(Identity(parameter, document.Root)))
                .Select(parameter => Name(Resolved(parameter, document.Root)))
                .OfType<string>(),
        ];
    }

    /// <summary>L'élément de chemin d'une opération, quand le document le porte.</summary>
    public static MappingNode? Item(SpecDocument document, OperationSelector selector)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(selector);

        return document.Root is MappingNode root
            && root.Find("paths") is MappingNode paths
            && paths.Find(selector.Path) is MappingNode item
                ? item
                : null;
    }

    /// <summary>
    /// Les paramètres de l'opération, puis ceux du chemin qu'elle ne masque pas.
    /// </summary>
    /// <remarks>
    /// Dans cet ordre, et c'est voulu : un relecteur voit d'abord ce que l'opération déclare pour
    /// elle-même. L'ordre n'a pas de sens pour OpenAPI, et la comparaison canonique trie de toute façon.
    /// </remarks>
    private static SequenceNode? Merged(MappingNode operation, MappingNode item, SpecNode root)
    {
        HashSet<string> own = new(Identities(operation, root), StringComparer.Ordinal);

        IReadOnlyList<SpecNode> inherited =
        [
            .. Items(item, Parameters).Where(parameter => !own.Contains(Identity(parameter, root))),
        ];

        IReadOnlyList<SpecNode> declared = Items(operation, Parameters);

        return declared.Count + inherited.Count == 0
            ? null
            : TreeEdits.Sequence(
                operation.Location.Child(Parameters), [.. declared, .. inherited]);
    }

    /// <summary>
    /// Le champ en vigueur : celui de l'opération, à défaut celui du chemin, à défaut celui du document.
    /// </summary>
    /// <remarks>
    /// Le plus proche gagne, et il remplace — il ne s'ajoute pas. C'est la règle d'OpenAPI pour
    /// <c>servers</c> comme pour <c>security</c>, et c'est ce qui fait qu'une liste de sécurité vide sur
    /// une opération annule l'exigence globale au lieu de s'y ajouter.
    /// </remarks>
    private static SpecNode? InForce(
        MappingNode operation,
        MappingNode item,
        MappingNode? root,
        string key) =>
        operation.Find(key) ?? item.Find(key) ?? root?.Find(key);

    /// <summary>Les identités nom-emplacement des paramètres qu'un objet déclare.</summary>
    private static IEnumerable<string> Identities(MappingNode node, SpecNode root) =>
        Items(node, Parameters).Select(parameter => Identity(parameter, root));

    /// <summary>
    /// L'identité d'un paramètre : son nom et son emplacement.
    /// </summary>
    /// <remarks>
    /// Les deux, parce qu'OpenAPI distingue un paramètre d'en-tête et un paramètre de requête portant le
    /// même nom. Comparer le seul nom ferait masquer l'un par l'autre.
    /// </remarks>
    private static string Identity(SpecNode parameter, SpecNode root)
    {
        SpecNode resolved = Resolved(parameter, root);

        return (Name(resolved) ?? "?") + "\u0000" + (Text(resolved, "in") ?? "?");
    }

    /// <summary>
    /// Le paramètre lui-même, la référence locale suivie quand c'en est une.
    /// </summary>
    /// <remarks>
    /// Nécessaire pour deux choses, et la seconde est la plus grave. D'abord nommer un paramètre hérité
    /// dans le rapport : une référence ne porte pas de nom, et ne pas la suivre laisserait la ligne vide.
    /// Ensuite comparer les identités : une opération qui redéclare sur place le paramètre que son chemin
    /// référence par <c>$ref</c> doit le masquer. Sans résolution, les deux écritures auraient des
    /// identités différentes et l'extrait en porterait deux là où le document n'en voit qu'un.
    /// </remarks>
    private static SpecNode Resolved(SpecNode parameter, SpecNode root)
    {
        if (parameter is not MappingNode mapping
            || mapping.Find("$ref") is not ScalarNode reference
            || !reference.Text.StartsWith("#/", StringComparison.Ordinal))
        {
            return parameter;
        }

        return PointerNavigator.Find(root, reference.Text[1..]) ?? parameter;
    }

    private static string? Name(SpecNode parameter) => Text(parameter, "name");

    private static string? Text(SpecNode node, string key) =>
        node is MappingNode mapping && mapping.Find(key) is ScalarNode scalar ? scalar.Text : null;

    private static IReadOnlyList<SpecNode> Items(MappingNode node, string key) =>
        node.Find(key) is SequenceNode sequence ? sequence.Items : [];

    private static bool Inherited(string key) =>
        string.Equals(key, Parameters, StringComparison.Ordinal)
        || string.Equals(key, Servers, StringComparison.Ordinal)
        || string.Equals(key, Security, StringComparison.Ordinal);

    private static void Add(List<MappingEntry> entries, string key, SpecNode? value)
    {
        if (value is not null)
        {
            entries.Add(new MappingEntry(TreeEdits.Key(key), value));
        }
    }
}
