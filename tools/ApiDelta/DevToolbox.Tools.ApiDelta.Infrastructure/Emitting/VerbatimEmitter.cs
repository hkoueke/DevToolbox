using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;

/// <summary>
/// Réécrit un document en réassemblant les étendues d'octets de son arbre.
/// </summary>
/// <remarks>
/// Ce qui est vérifié ici n'est pas que l'on sait recopier un fichier — ce serait sans intérêt — mais que
/// l'arbre <em>couvre</em> le document : chaque nœud porte l'étendue exacte qu'il occupait, et leur
/// réassemblage reproduit l'entrée. Un nœud oublié, une étendue mal calculée, une clé perdue se voient
/// immédiatement comme une différence d'octets.
/// <para>
/// C'est aussi la forme que prendra l'écriture d'un document modifié : réassembler les étendues
/// inchangées autour de ce qui a changé est ce qui rend atteignable, plus tard, une réécriture préservant
/// les commentaires — que le modèle sémantique d'une bibliothèque, lui, ne saurait pas conserver.
/// </para>
/// </remarks>
public sealed class VerbatimEmitter : IVerbatimEmitter
{
    /// <inheritdoc />
    public ReadOnlyMemory<byte> Emit(SpecDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Le document peut comporter un prologue et un épilogue qu'aucun nœud ne couvre : une directive
        // YAML, un marqueur de début de document, des commentaires, la fin de ligne finale. Ils font
        // partie du fichier, et un réassemblage qui les perdrait ne serait pas fidèle.
        ByteSpan root = document.Root.Span;

        ReadOnlySpan<byte> source = document.Source.Span;

        byte[] rebuilt = new byte[source.Length];

        source[..(int)root.Start].CopyTo(rebuilt);

        int written = Write(document.Root, source, rebuilt, (int)root.Start);

        source[written..].CopyTo(rebuilt.AsSpan(written));

        return rebuilt;
    }

    /// <summary>Écrit un nœud, puis ce qui suit jusqu'à la fin de son étendue.</summary>
    /// <returns>Le décalage atteint après écriture.</returns>
    private static int Write(SpecNode node, ReadOnlySpan<byte> source, byte[] destination, int position)
    {
        return node switch
        {
            MappingNode mapping => WriteMapping(mapping, source, destination, position),
            SequenceNode sequence => WriteSequence(sequence, source, destination, position),
            _ => WriteSpan(node.Span, source, destination, position),
        };
    }

    private static int WriteMapping(
        MappingNode mapping,
        ReadOnlySpan<byte> source,
        byte[] destination,
        int position)
    {
        int cursor = position;

        foreach (MappingEntry entry in mapping.Entries)
        {
            cursor = WriteGap(entry.Key.Span, source, destination, cursor);
            cursor = WriteSpan(entry.Key.Span, source, destination, cursor);
            cursor = WriteGap(entry.Value.Span, source, destination, cursor);
            cursor = Write(entry.Value, source, destination, cursor);
        }

        return WriteUpTo((int)mapping.Span.End, source, destination, cursor);
    }

    private static int WriteSequence(
        SequenceNode sequence,
        ReadOnlySpan<byte> source,
        byte[] destination,
        int position)
    {
        int cursor = position;

        foreach (SpecNode item in sequence.Items)
        {
            cursor = WriteGap(item.Span, source, destination, cursor);
            cursor = Write(item, source, destination, cursor);
        }

        return WriteUpTo((int)sequence.Span.End, source, destination, cursor);
    }

    /// <summary>
    /// Recopie ce qui sépare le curseur du prochain nœud : indentation, tirets, deux-points, virgules,
    /// accolades, commentaires. La structure du document vit là, entre les nœuds.
    /// </summary>
    private static int WriteGap(ByteSpan next, ReadOnlySpan<byte> source, byte[] destination, int cursor) =>
        WriteUpTo((int)next.Start, source, destination, cursor);

    private static int WriteSpan(ByteSpan span, ReadOnlySpan<byte> source, byte[] destination, int cursor)
    {
        int start = Math.Max(cursor, (int)span.Start);

        return WriteUpTo((int)span.End, source, destination, start);
    }

    private static int WriteUpTo(int end, ReadOnlySpan<byte> source, byte[] destination, int cursor)
    {
        if (end <= cursor)
        {
            return cursor;
        }

        int bounded = Math.Min(end, source.Length);

        source[cursor..bounded].CopyTo(destination.AsSpan(cursor));

        return bounded;
    }
}
