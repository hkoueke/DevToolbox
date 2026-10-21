using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using CoreScalarStyle = DevToolbox.Tools.ApiDelta.Core.Syntax.ScalarStyle;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Yaml;

/// <summary>
/// Construit l'arbre de syntaxe à partir du flux d'événements de l'analyseur YAML.
/// </summary>
/// <remarks>
/// L'analyse se fait au niveau des événements, et non par un modèle d'objets : c'est le seul niveau qui
/// donne la position, l'étendue en octets et le style d'écriture de chaque nœud, et c'est aussi le seul où
/// une clé en double soit encore visible. Un modèle, quel qu'il soit, l'aurait déjà résolue dans un sens
/// ou dans l'autre.
/// <para>
/// Les clés en double sont relevées <em>toutes</em>, avec les deux positions de chacune, parce qu'un
/// développeur qui en a laissé passer une en a souvent laissé passer plusieurs.
/// </para>
/// </remarks>
internal sealed class YamlTreeBuilder
{
    private readonly IParser _parser;
    private readonly LoadLimits _limits;
    private readonly ByteOffsets _offsets;
    private readonly List<Diagnostic> _diagnostics = [];

    private int _aliasExpansions;

    internal YamlTreeBuilder(IParser parser, LoadLimits limits, ByteOffsets offsets)
    {
        _parser = parser;
        _limits = limits;
        _offsets = offsets;
    }

    internal IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>Construit l'arbre du premier document du flux.</summary>
    /// <returns>La racine, ou <see langword="null"/> si le flux ne porte aucun document.</returns>
    internal SpecNode? Build()
    {
        _parser.Consume<StreamStart>();

        if (!_parser.TryConsume<DocumentStart>(out _))
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "Ce fichier ne contient aucun document YAML."));

            return null;
        }

        SpecNode root = BuildNode(SpecPointer.Root, depth: 0);

        // Un deuxième document dans le même flux : OpenAPI n'en décrit qu'un, et deviner lequel compte
        // serait choisir à la place de l'auteur.
        if (_parser.TryConsume<DocumentEnd>(out _) && _parser.Accept<DocumentStart>(out DocumentStart? second))
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "Ce fichier contient plusieurs documents YAML ; une description OpenAPI en compte un.",
                SpecPointer.Root,
                At(second.Start)));
        }

        return root;
    }

    private SpecNode BuildNode(SpecPointer location, int depth)
    {
        if (depth > _limits.MaxDepth)
        {
            throw new LoadLimitReachedException(
                $"L'imbrication dépasse la limite de {_limits.MaxDepth} niveaux.",
                CurrentPosition(),
                location);
        }

        if (_parser.Accept<MappingStart>(out _))
        {
            return BuildMapping(location, depth);
        }

        if (_parser.Accept<SequenceStart>(out _))
        {
            return BuildSequence(location, depth);
        }

        if (_parser.TryConsume<AnchorAlias>(out AnchorAlias? alias))
        {
            // Un alias est une expansion, et une expansion compte dans le budget : un document qui en
            // enchaîne peut peser à la lecture bien plus que sur le disque.
            if (++_aliasExpansions > _limits.MaxAliasExpansions)
            {
                throw new LoadLimitReachedException(
                    $"Le nombre d'expansions d'alias dépasse la limite de {_limits.MaxAliasExpansions}.",
                    At(alias.Start),
                    location);
            }

            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.ParseFailed,
                DiagnosticSeverity.Error,
                "Les alias YAML ne sont pas pris en charge : l'outil ne réécrirait pas un alias qu'il "
                    + "n'a pas lu, et le développer en silence changerait le document qu'il décrit.",
                location,
                At(alias.Start)));

            return Scalar(location, alias.Start, alias.End, string.Empty, CoreScalarStyle.Plain);
        }

        Scalar scalar = _parser.Consume<Scalar>();

        return Scalar(location, scalar.Start, scalar.End, scalar.Value, Translate(scalar.Style), TagOf(scalar));
    }

    private MappingNode BuildMapping(SpecPointer location, int depth)
    {
        MappingStart start = _parser.Consume<MappingStart>();

        List<MappingEntry> entries = [];
        Dictionary<string, SourcePosition> seen = new(StringComparer.Ordinal);

        while (!_parser.Accept<MappingEnd>(out _))
        {
            if (!_parser.Accept<Scalar>(out _))
            {
                // OpenAPI n'a pas de clé composite : une clé qui n'est pas un scalaire est une erreur de
                // structure, non une subtilité à interpréter.
                SourcePosition position = CurrentPosition();

                _diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.NonStringKey,
                    DiagnosticSeverity.Error,
                    "Cette clé n'est pas une chaîne ; une description OpenAPI n'en comporte pas.",
                    location,
                    position));

                _parser.SkipThisAndNestedEvents();
                _parser.SkipThisAndNestedEvents();
                continue;
            }

            Scalar key = _parser.Consume<Scalar>();
            ScalarNode keyNode = Scalar(
                location.Child(key.Value), key.Start, key.End, key.Value, Translate(key.Style), TagOf(key));

            RecordDuplicate(seen, key, location);

            SpecNode value = BuildNode(location.Child(key.Value), depth + 1);

            entries.Add(new MappingEntry(keyNode, value));
        }

        MappingEnd end = _parser.Consume<MappingEnd>();

        return new MappingNode(location, At(start.Start), Span(start.Start, end.End), entries);
    }

    private SequenceNode BuildSequence(SpecPointer location, int depth)
    {
        SequenceStart start = _parser.Consume<SequenceStart>();

        List<SpecNode> items = [];

        while (!_parser.Accept<SequenceEnd>(out _))
        {
            items.Add(BuildNode(location.Item(items.Count), depth + 1));
        }

        SequenceEnd end = _parser.Consume<SequenceEnd>();

        return new SequenceNode(location, At(start.Start), Span(start.Start, end.End), items);
    }

    /// <summary>Relève une clé déjà vue, en nommant les deux positions.</summary>
    private void RecordDuplicate(
        Dictionary<string, SourcePosition> seen,
        Scalar key,
        SpecPointer location)
    {
        SourcePosition position = At(key.Start);

        if (seen.TryGetValue(key.Value, out SourcePosition? first))
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.DuplicateKey,
                DiagnosticSeverity.Error,
                $"La clé « {key.Value} » apparaît deux fois. La plupart des analyseurs n'en gardent "
                    + "qu'une, sans le dire : la description est donc refusée plutôt que lue de travers.",
                location.Child(key.Value),
                position,
                [first]));

            return;
        }

        seen[key.Value] = position;
    }

    private ScalarNode Scalar(
        SpecPointer location,
        Mark start,
        Mark end,
        string text,
        CoreScalarStyle style,
        string? tag = null) =>
        new(
            location,
            At(start),
            Span(start, end),
            text,
            style,
            style == CoreScalarStyle.Plain
                ? CoreSchemaResolver.ResolvePlain(text)
                : CoreSchemaResolver.ResolveQuoted(text),
            tag);

    /// <summary>
    /// La position de l'événement courant. L'analyseur peut n'en avoir aucun — flux épuisé — et une
    /// position nulle vaut mieux qu'un diagnostic perdu.
    /// </summary>
    /// <summary>
    /// L'étiquette explicite d'un nœud, ou rien.
    /// </summary>
    /// <remarks>
    /// Une étiquette non spécifique — <c>?</c> ou <c>!</c> — n'est pas une étiquette : c'est l'absence
    /// d'étiquette, que YAML note explicitement. La lire comme une valeur lève.
    /// </remarks>
    private static string? TagOf(NodeEvent node) =>
        node.Tag.IsEmpty || node.Tag.IsNonSpecific ? null : node.Tag.Value;

    private SourcePosition CurrentPosition() =>
        _parser.Current is { } current ? At(current.Start) : new SourcePosition(0, 0);

    private static SourcePosition At(Mark mark) => new((int)mark.Line, (int)mark.Column);

    /// <summary>L'étendue en octets d'un nœud, traduite depuis les rangs de caractères de l'analyseur.</summary>
    private ByteSpan Span(Mark start, Mark end)
    {
        long from = _offsets.Of(start.Index);

        return new ByteSpan(from, _offsets.Of(end.Index) - from);
    }

    private static CoreScalarStyle Translate(YamlDotNet.Core.ScalarStyle style) => style switch
    {
        YamlDotNet.Core.ScalarStyle.SingleQuoted => CoreScalarStyle.SingleQuoted,
        YamlDotNet.Core.ScalarStyle.DoubleQuoted => CoreScalarStyle.DoubleQuoted,
        YamlDotNet.Core.ScalarStyle.Literal => CoreScalarStyle.Literal,
        YamlDotNet.Core.ScalarStyle.Folded => CoreScalarStyle.Folded,
        _ => CoreScalarStyle.Plain,
    };
}
