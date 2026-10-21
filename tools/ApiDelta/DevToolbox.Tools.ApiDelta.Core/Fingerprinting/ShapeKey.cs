using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Fingerprinting;

/// <summary>Une clé de forme, assez grossière pour rassembler des candidats comparables.</summary>
/// <param name="Kind">La sorte du nœud.</param>
/// <param name="Properties">Les noms de ses clés, triés et joints.</param>
/// <param name="ChildCount">Le nombre d'enfants.</param>
/// <param name="SizeBand">Sa taille, par ordre de grandeur.</param>
/// <remarks>
/// Cette clé ne prouve rien : deux candidats qui la partagent ne sont pas pour autant semblables. Elle
/// sert à ne pas comparer tout avec tout, ce qui est la différence entre quelques milliers de
/// comparaisons et cinquante millions.
/// </remarks>
public sealed record ShapeKey(string Kind, string Properties, int ChildCount, int SizeBand)
{
    /// <summary>La clé de forme d'un nœud.</summary>
    /// <param name="node">Le nœud.</param>
    /// <returns>Sa clé.</returns>
    public static ShapeKey Of(SpecNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node switch
        {
            MappingNode mapping => new ShapeKey(
                "mapping",
                string.Join(
                    ',',
                    mapping.Entries
                        .Select(entry => entry.Key.Text)
                        .OrderBy(key => key, StringComparer.Ordinal)),
                mapping.Entries.Count,
                Band(node.Span.Length)),

            SequenceNode sequence => new ShapeKey(
                "sequence", string.Empty, sequence.Items.Count, Band(node.Span.Length)),

            _ => new ShapeKey("scalar", string.Empty, 0, Band(node.Span.Length)),
        };
    }

    /// <summary>L'ordre de grandeur d'une taille, en puissances de deux.</summary>
    private static int Band(long length) =>
        length <= 0 ? 0 : (int)Math.Log2(length);
}
