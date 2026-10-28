using System.Text.Json;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Json;

/// <summary>
/// Construit l'arbre de syntaxe à partir d'un document JSON.
/// </summary>
/// <remarks>
/// Le même arbre que celui du lecteur YAML, construit au même niveau de détail : positions, étendues et
/// clés en double. JSON n'a pas de style de scalaire au sens de YAML — une chaîne y est toujours entre
/// guillemets — si bien que les styles se réduisent à deux cas, mais l'audit de mise en forme garde de
/// quoi distinguer un code de réponse quoté d'un autre.
/// <para>
/// La spécification JSON ne dit rien des clés en double, et la plateforme n'en garde qu'une : elles sont
/// donc relevées ici, toutes, avant que rien ne les lie.
/// </para>
/// </remarks>
internal sealed class JsonTreeBuilder
{
    private readonly LineIndex _lines;
    private readonly LoadLimits _limits;
    private readonly long _baseOffset;
    private readonly List<Diagnostic> _diagnostics = [];

    /// <summary>Crée le constructeur.</summary>
    /// <param name="lines">Situe un décalage en ligne et colonne, dans le document analysé.</param>
    /// <param name="limits">Le budget de lecture.</param>
    /// <param name="baseOffset">
    /// Le décalage, dans le fichier, du premier octet analysé. Vaut trois lorsque le fichier porte une
    /// nomenclature d'octets : l'analyseur lit après elle, tandis que les étendues doivent désigner le
    /// fichier réel, parce que c'est sur lui que l'épissure écrit.
    /// </param>
    internal JsonTreeBuilder(LineIndex lines, LoadLimits limits, long baseOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseOffset);

        _lines = lines;
        _limits = limits;
        _baseOffset = baseOffset;
    }

    internal IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>Construit l'arbre du document.</summary>
    /// <param name="content">Les octets du document.</param>
    /// <returns>La racine.</returns>
    internal SpecNode Build(ReadOnlySpan<byte> content)
    {
        Utf8JsonReader reader = new(
            content,
            new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = false,
                MaxDepth = _limits.MaxDepth,
            });

        reader.Read();

        return BuildNode(ref reader, SpecPointer.Root, depth: 0);
    }

    private SpecNode BuildNode(ref Utf8JsonReader reader, SpecPointer location, int depth)
    {
        if (depth > _limits.MaxDepth)
        {
            throw new Yaml.LoadLimitReachedException(
                $"L'imbrication dépasse la limite de {_limits.MaxDepth} niveaux.",
                _lines.At(reader.TokenStartIndex),
                location);
        }

        return reader.TokenType switch
        {
            JsonTokenType.StartObject => BuildMapping(ref reader, location, depth),
            JsonTokenType.StartArray => BuildSequence(ref reader, location, depth),
            _ => BuildScalar(ref reader, location),
        };
    }

    private MappingNode BuildMapping(ref Utf8JsonReader reader, SpecPointer location, int depth)
    {
        long start = reader.TokenStartIndex;

        List<MappingEntry> entries = [];
        Dictionary<string, SourcePosition> seen = new(StringComparer.Ordinal);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string key = reader.GetString() ?? string.Empty;
            long keyStart = reader.TokenStartIndex;
            SourcePosition keyPosition = _lines.At(keyStart);

            RecordDuplicate(seen, key, keyPosition, location);

            ScalarNode keyNode = new(
                location.Child(key),
                keyPosition,
                new ByteSpan(keyStart + _baseOffset, reader.BytesConsumed - keyStart),
                key,
                ScalarStyle.DoubleQuoted,
                CoreSchemaResolver.ResolveQuoted(key));

            reader.Read();

            entries.Add(new MappingEntry(keyNode, BuildNode(ref reader, location.Child(key), depth + 1)));
        }

        return new MappingNode(location, _lines.At(start), Extent(start, ref reader), entries);
    }

    private SequenceNode BuildSequence(ref Utf8JsonReader reader, SpecPointer location, int depth)
    {
        long start = reader.TokenStartIndex;

        List<SpecNode> items = [];

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            items.Add(BuildNode(ref reader, location.Item(items.Count), depth + 1));
        }

        return new SequenceNode(location, _lines.At(start), Extent(start, ref reader), items);
    }

    private ScalarNode BuildScalar(ref Utf8JsonReader reader, SpecPointer location)
    {
        long start = reader.TokenStartIndex;

        (string text, ResolvedScalar resolved, ScalarStyle style) = reader.TokenType switch
        {
            JsonTokenType.String => (
                reader.GetString() ?? string.Empty,
                CoreSchemaResolver.ResolveQuoted(reader.GetString() ?? string.Empty),
                ScalarStyle.DoubleQuoted),

            JsonTokenType.Number => Number(ref reader),

            JsonTokenType.True => ("true", new ResolvedScalar(ScalarValueKind.Boolean, "true"), ScalarStyle.Plain),
            JsonTokenType.False => ("false", new ResolvedScalar(ScalarValueKind.Boolean, "false"), ScalarStyle.Plain),

            _ => ("null", new ResolvedScalar(ScalarValueKind.Null, "null"), ScalarStyle.Plain),
        };

        return new ScalarNode(
            location, _lines.At(start), Extent(start, ref reader), text, style, resolved);
    }

    /// <summary>
    /// Lit un nombre tel qu'il était écrit, puis le met sous forme canonique.
    /// </summary>
    /// <remarks>
    /// Les deux comptent : l'écriture, parce qu'un audit de mise en forme porte sur elle ; la forme
    /// canonique, parce que <c>1.0</c> et <c>1</c> sont la même contrainte et doivent partager une
    /// empreinte.
    /// </remarks>
    private static (string Text, ResolvedScalar Resolved, ScalarStyle Style) Number(
        ref Utf8JsonReader reader)
    {
        // ValueSpan suffit : un nombre JSON ne traverse jamais la frontière d'un segment de lecture,
        // puisque le document est lu depuis un unique tampon.
        string written = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);

        return (written, CoreSchemaResolver.ResolvePlain(written), ScalarStyle.Plain);
    }

    private void RecordDuplicate(
        Dictionary<string, SourcePosition> seen,
        string key,
        SourcePosition position,
        SpecPointer location)
    {
        if (seen.TryGetValue(key, out SourcePosition? first))
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.DuplicateKey,
                DiagnosticSeverity.Error,
                $"La propriété « {key} » apparaît deux fois. JSON ne dit pas laquelle compte et la "
                    + "plateforme n'en garde qu'une : la description est donc refusée plutôt que lue de "
                    + "travers.",
                location.Child(key),
                position,
                [first]));

            return;
        }

        seen[key] = position;
    }

    private ByteSpan Extent(long start, ref Utf8JsonReader reader) =>
        new(start + _baseOffset, reader.BytesConsumed - start);
}
