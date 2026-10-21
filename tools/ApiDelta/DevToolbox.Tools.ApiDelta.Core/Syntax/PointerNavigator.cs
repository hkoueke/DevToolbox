namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>
/// Retrouve un nœud à partir d'un pointeur JSON.
/// </summary>
/// <remarks>
/// C'est ce qui permet de ramener dans l'arbre un verdict exprimé par pointeur — celui d'une bibliothèque
/// de validation, celui d'une référence — et donc de lui donner une ligne et une colonne. Sans cela, une
/// erreur de structure se lirait « quelque part dans le document ».
/// </remarks>
public static class PointerNavigator
{
    /// <summary>Retrouve le nœud désigné par un pointeur.</summary>
    /// <param name="root">La racine du document.</param>
    /// <param name="jsonPointer">Le pointeur, sous la forme <c>/paths/~1documents/get</c>.</param>
    /// <returns>Le nœud, ou <see langword="null"/> si le pointeur ne désigne rien.</returns>
    public static SpecNode? Find(SpecNode root, string? jsonPointer)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (string.IsNullOrEmpty(jsonPointer) || string.Equals(jsonPointer, "/", StringComparison.Ordinal))
        {
            return root;
        }

        SpecNode? current = root;

        foreach (string raw in jsonPointer.TrimStart('/').Split('/'))
        {
            if (current is null)
            {
                return null;
            }

            current = Descend(current, Unescape(raw));
        }

        return current;
    }

    /// <summary>
    /// Défait l'échappement de la RFC 6901. L'ordre compte : <c>~1</c> d'abord, <c>~0</c> ensuite, sans
    /// quoi un segment contenant <c>~01</c> se relirait comme une barre oblique.
    /// </summary>
    private static string Unescape(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static SpecNode? Descend(SpecNode current, string segment) => current switch
    {
        MappingNode mapping => mapping.Find(segment),
        SequenceNode sequence => Item(sequence, segment),
        _ => null,
    };

    private static SpecNode? Item(SequenceNode sequence, string segment) =>
        int.TryParse(segment, System.Globalization.CultureInfo.InvariantCulture, out int index)
            && index >= 0
            && index < sequence.Items.Count
                ? sequence.Items[index]
                : null;
}
