using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>
/// Parcourt les chemins une fois et rend ce qu'il faut pour choisir.
/// </summary>
/// <remarks>
/// Une passe, et rien de plus que des chaînes courtes. Le catalogue est ce que l'écran de sélection
/// affiche, et sur une description de quatre cents opérations il doit s'ouvrir aussitôt : y faire voyager
/// les sous-arbres rendrait le simple fait de regarder la liste aussi cher que l'examen complet.
/// <para>
/// Ce qui n'est pas une méthode dans un élément de chemin est un champ — <c>parameters</c>,
/// <c>summary</c>, <c>servers</c> — et les confondre produirait des opérations fantômes qu'aucune
/// sélection ne pourrait résoudre.
/// </para>
/// </remarks>
public sealed class OperationCatalog : IOperationCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<SelectableOperation> List(SpecDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Root is not MappingNode root || root.Find("paths") is not MappingNode paths)
        {
            return [];
        }

        List<SelectableOperation> operations = [];

        foreach (MappingEntry path in paths.Entries)
        {
            if (path.Value is not MappingNode item)
            {
                continue;
            }

            operations.AddRange(Of(path.Key.Text, item));
        }

        return
        [
            .. operations
                .OrderBy(operation => operation.Selector.Path, StringComparer.Ordinal)
                .ThenBy(operation => operation.Selector.MethodKey, StringComparer.Ordinal),
        ];
    }

    /// <summary>Les opérations d'un élément de chemin.</summary>
    private static IEnumerable<SelectableOperation> Of(string path, MappingNode item)
    {
        foreach (MappingEntry entry in item.Entries)
        {
            if (!PathRows.Methods.Contains(entry.Key.Text)
                || !Enum.TryParse(entry.Key.Text, ignoreCase: true, out HttpOperationMethod method)
                || entry.Value is not MappingNode operation)
            {
                continue;
            }

            yield return new SelectableOperation(
                new OperationSelector(path, method),
                Text(operation, "summary") ?? Text(operation, "description"),
                Tags(operation),
                Flag(operation, "deprecated"),
                entry.Value.Location);
        }
    }

    /// <summary>Les étiquettes d'une opération, qui servent à filtrer et à grouper.</summary>
    private static IReadOnlyList<string> Tags(MappingNode operation) =>
        operation.Find("tags") is SequenceNode tags
            ? [.. tags.Items.OfType<ScalarNode>().Select(tag => tag.Text)]
            : [];

    /// <summary>
    /// Le texte d'un champ, abrégé.
    /// </summary>
    /// <remarks>
    /// Abrégé parce que c'est une ligne d'écran : une description de huit cents mots recopiée dans le
    /// catalogue annulerait l'économie que le catalogue existe pour faire.
    /// </remarks>
    private static string? Text(MappingNode operation, string key)
    {
        const int MaxLength = 80;

        if (operation.Find(key) is not ScalarNode scalar
            || string.IsNullOrWhiteSpace(scalar.Text))
        {
            return null;
        }

        string text = scalar.Text.ReplaceLineEndings(" ").Trim();

        return text.Length <= MaxLength ? text : text[..MaxLength] + "…";
    }

    private static bool Flag(MappingNode operation, string key) =>
        operation.Find(key) is ScalarNode { Resolved.Canonical: "true" };
}
