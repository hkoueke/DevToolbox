using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-008 : remonte au niveau du chemin les en-têtes que toutes ses opérations portent à l'identique.
/// </summary>
/// <remarks>
/// La condition est stricte, et elle est vérifiée ici plutôt que supposée : un en-tête déclaré au niveau
/// du chemin vaut pour <em>toutes</em> ses opérations, y compris celles qui ne le portaient pas. La
/// remontée n'a donc lieu que sur un chemin dont chaque opération figure dans la proposition. Un chemin
/// qui n'y satisfait pas est laissé intact, et si aucun n'y satisfait la transformation échoue plutôt que
/// de passer pour faite.
/// <para>
/// C'est la seule règle d'extraction qui touche à la portée, et c'est la raison pour laquelle elle est
/// distincte d'OPT-002 : celle-ci remplace sur place et ne change donc jamais ce qui s'applique à quoi.
/// </para>
/// </remarks>
public sealed class HoistPathParametersRule : IOptimizationRule
{
    private const string Parameters = "parameters";

    /// <inheritdoc />
    public string RuleId => "OPT-008";

    /// <inheritdoc />
    public bool EnabledByDefault => OptimizationDefaults.EnabledByDefault(RuleId);

    /// <inheritdoc />
    public bool Removes => false;

    /// <inheritdoc />
    public Result<SpecNode> Apply(SpecNode root, Transformation transformation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(transformation);

        SpecNode edited = root;
        int hoisted = 0;

        foreach (IGrouping<string, SpecPointer> path in transformation.Origins
            .Where(origin => origin.Segments.Count == 3)
            .GroupBy(origin => origin.Segments[1], StringComparer.Ordinal))
        {
            if (Hoist(edited, path.Key, [.. path]) is { } result)
            {
                edited = result;
                hoisted++;
            }
        }

        return hoisted == 0
            ? Result.Fail<SpecNode>(
                FailureReason.InvalidInput,
                "Aucun chemin ne porte ces en-têtes sur toutes ses opérations : les remonter les "
                + "ajouterait à des opérations qui ne les déclaraient pas.")
            : Result.Success(edited);
    }

    /// <summary>Remonte les en-têtes d'un chemin, quand toutes ses opérations y figurent.</summary>
    private static SpecNode? Hoist(
        SpecNode root,
        string path,
        IReadOnlyList<SpecPointer> operations)
    {
        SpecPointer item = SpecPointer.Root.Child("paths").Child(path);

        if (PointerNavigator.Find(root, item.Json) is not MappingNode pathItem
            || !Complete(pathItem, operations))
        {
            return null;
        }

        IReadOnlyList<SpecNode> headers = Headers(root, operations[0]);

        if (headers.Count == 0)
        {
            return null;
        }

        SpecNode edited = root;

        foreach (SpecPointer operation in operations)
        {
            edited = Strip(edited, operation);
        }

        return Append(edited, item, headers);
    }

    /// <summary>Indique si chaque opération du chemin figure dans la proposition.</summary>
    private static bool Complete(MappingNode pathItem, IReadOnlyList<SpecPointer> operations)
    {
        HashSet<string> named = new(
            operations.Select(operation => operation.Segments[2]), StringComparer.Ordinal);

        return pathItem.Entries
            .Select(entry => entry.Key.Text)
            .Where(PathRows.Methods.Contains)
            .All(named.Contains);
    }

    /// <summary>Les paramètres d'en-tête écrits sur place par une opération.</summary>
    private static IReadOnlyList<SpecNode> Headers(SpecNode root, SpecPointer operation) =>
        PointerNavigator.Find(root, operation.Json) is MappingNode node
            && node.Find(Parameters) is SequenceNode written
                ? [.. written.Items.Where(IsHeader)]
                : [];

    private static bool IsHeader(SpecNode parameter) =>
        parameter is MappingNode mapping
        && mapping.Find("in") is ScalarNode where
        && string.Equals(where.Text, "header", StringComparison.Ordinal);

    /// <summary>
    /// Retire les en-têtes écrits sur place d'une opération, et la clé elle-même si elle se vide.
    /// </summary>
    /// <remarks>
    /// Les rangs décroissants, parce que retirer un élément décale ceux qui suivent. Retirer dans
    /// l'ordre croissant retirerait le mauvais à partir du deuxième.
    /// </remarks>
    private static SpecNode Strip(SpecNode root, SpecPointer operation)
    {
        if (PointerNavigator.Find(root, operation.Json) is not MappingNode node
            || node.Find(Parameters) is not SequenceNode written)
        {
            return root;
        }

        SpecPointer list = operation.Child(Parameters);
        SpecNode edited = root;

        for (int rank = written.Items.Count - 1; rank >= 0; rank--)
        {
            if (IsHeader(written.Items[rank]))
            {
                edited = TreeEdits.Remove(edited, list.Item(rank));
            }
        }

        return PointerNavigator.Find(edited, list.Json) is SequenceNode { Items.Count: 0 }
            ? TreeEdits.Remove(edited, list)
            : edited;
    }

    /// <summary>Ajoute les en-têtes aux paramètres du chemin, en gardant ceux qui y étaient.</summary>
    private static SpecNode Append(
        SpecNode root,
        SpecPointer item,
        IReadOnlyList<SpecNode> headers)
    {
        SpecPointer list = item.Child(Parameters);

        IReadOnlyList<SpecNode> existing =
            PointerNavigator.Find(root, list.Json) is SequenceNode written ? written.Items : [];

        return TreeEdits.Insert(root, list, TreeEdits.Sequence(list, [.. existing, .. headers]));
    }
}
