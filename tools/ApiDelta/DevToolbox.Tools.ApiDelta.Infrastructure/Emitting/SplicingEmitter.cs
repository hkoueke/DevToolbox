using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;

/// <summary>
/// Réécrit un document en recopiant ses octets partout, sauf aux endroits désignés.
/// </summary>
/// <remarks>
/// Toute la valeur est dans le « sauf » : ce qui n'a pas été désigné n'est pas réécrit, il est recopié.
/// Commentaires, indentation, ordre des clés, quotation de la production survivent donc intacts, et le
/// diff que lit un relecteur ne contient que ce qui a été demandé.
/// <para>
/// Le format YAML est exigé. Une production écrite en JSON serait entièrement reformatée pour qu'on y
/// insère quoi que ce soit — l'émetteur canonique de cet outil n'écrit que du YAML — et une réécriture
/// complète enterrerait le changement voulu sous des milliers de lignes. Mieux vaut refuser en le disant.
/// </para>
/// </remarks>
public sealed class SplicingEmitter : ISplicingEmitter
{
    private readonly ISpecEmitter _emitter;

    /// <summary>Crée l'épisseur.</summary>
    /// <param name="emitter">Écrit les parties neuves, et elles seules.</param>
    public SplicingEmitter(ISpecEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(emitter);

        _emitter = emitter;
    }

    /// <inheritdoc />
    public Result<ReadOnlyMemory<byte>> Splice(
        SpecDocument document,
        IReadOnlyList<Splice> splices,
        CanonicalFormatting formatting)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(splices);
        ArgumentNullException.ThrowIfNull(formatting);

        if (document.Format is not SpecFormat.Yaml)
        {
            return Result.Fail<ReadOnlyMemory<byte>>(
                FailureReason.InvalidInput,
                "La production consolidée est écrite en recopiant les octets de la production autour de ce "
                    + "qui change, ce qui demande une production écrite en YAML. Une production en JSON "
                    + "devrait être reformatée en entier, et le changement demandé disparaîtrait dans la "
                    + "réécriture.");
        }

        List<Edit> edits = [];

        foreach (Splice splice in splices)
        {
            Result<Edit> edit = Resolve(document, splice, formatting);

            if (edit.IsFailure)
            {
                return Result.Fail<ReadOnlyMemory<byte>>(edit.Failure!);
            }

            edits.Add(edit.Value);
        }

        return Apply(document, edits);
    }

    /// <summary>Assemble la sortie : les octets d'origine, coupés aux endroits des épissures.</summary>
    private static Result<ReadOnlyMemory<byte>> Apply(SpecDocument document, List<Edit> edits)
    {
        edits.Sort((left, right) => left.Start.CompareTo(right.Start));

        for (int at = 1; at < edits.Count; at++)
        {
            if (edits[at].Start < edits[at - 1].Start + edits[at - 1].Length)
            {
                return Result.Fail<ReadOnlyMemory<byte>>(
                    FailureReason.InvalidInput,
                    "Deux des changements demandés portent sur des étendues qui se recouvrent : les "
                        + "appliquer tous les deux écrirait l'un par-dessus l'autre.");
            }
        }

        ReadOnlySpan<byte> source = document.Source.Span;
        List<byte> output = new(source.Length);
        int cursor = 0;

        foreach (Edit edit in edits)
        {
            output.AddRange(source[cursor..edit.Start]);
            output.AddRange(edit.Replacement);
            cursor = edit.Start + edit.Length;
        }

        output.AddRange(source[cursor..]);

        return Result.Success<ReadOnlyMemory<byte>>(output.ToArray());
    }

    /// <summary>Traduit une épissure en une étendue d'octets et le texte qui la remplace.</summary>
    private Result<Edit> Resolve(SpecDocument document, Splice splice, CanonicalFormatting formatting)
    {
        SpecNode? node = PointerNavigator.Find(document.Root, splice.Target.Json);

        if (node is null)
        {
            return Result.Fail<Edit>(
                FailureReason.InvalidInput,
                "« " + splice.Target.Display + " » n'existe pas dans la production : il n'y a donc rien "
                    + "à y modifier.");
        }

        return splice.Kind switch
        {
            SpliceKind.Replace => Replacing(document, node, splice, formatting),
            SpliceKind.Add => Adding(document, node, splice, formatting),
            SpliceKind.Remove => Removing(document, node),
            _ => Result.Fail<Edit>(FailureReason.InvalidInput, "Épissure de sorte inconnue."),
        };
    }

    /// <summary>Remplace la valeur d'un emplacement par une autre.</summary>
    private Result<Edit> Replacing(
        SpecDocument document,
        SpecNode node,
        Splice splice,
        CanonicalFormatting formatting)
    {
        if (splice.Value is null)
        {
            return Result.Fail<Edit>(
                FailureReason.InvalidInput, "Un remplacement sans valeur ne remplace rien.");
        }

        string rendered = Render(splice.Value, formatting, node.Position.Column - 1);
        int start = (int)node.Span.Start;

        return Result.Success(new Edit(
            start, ContentEnd(document, node) - start, Encoding.UTF8.GetBytes(rendered)));
    }

    /// <summary>Ajoute une entrée sous une association, après sa dernière entrée.</summary>
    private Result<Edit> Adding(
        SpecDocument document,
        SpecNode node,
        Splice splice,
        CanonicalFormatting formatting)
    {
        if (splice.Value is null || string.IsNullOrWhiteSpace(splice.Key))
        {
            return Result.Fail<Edit>(
                FailureReason.InvalidInput, "Un ajout demande une clé et une valeur.");
        }

        if (node is not MappingNode mapping || mapping.Entries.Count == 0)
        {
            return Result.Fail<Edit>(
                FailureReason.InvalidInput,
                "« " + splice.Target.Display + " » n'est pas une association pourvue d'au moins une "
                    + "entrée : l'outil ne peut pas en déduire à quelle indentation écrire la nouvelle.");
        }

        int indent = mapping.Entries[0].Key.Position.Column - 1;
        int insertion = ContentEnd(document, mapping.Entries[^1].Value);

        string text = "\n" + new string(' ', indent) + splice.Key + ":\n";
        text += Render(splice.Value, formatting, indent + formatting.Indent, firstLineIndented: true);

        return Result.Success(new Edit(insertion, 0, Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Retire une entrée, sa clé et sa ligne comprises.</summary>
    private static Result<Edit> Removing(SpecDocument document, SpecNode node)
    {
        ReadOnlySpan<byte> source = document.Source.Span;
        int start = (int)node.Span.Start;

        // Remonter jusqu'au début de la ligne qui porte la clé : retirer la valeur seule laisserait une
        // clé orpheline, c'est-à-dire un document que plus rien ne relit.
        while (start > 0 && source[start - 1] is not (byte)'\n')
        {
            start--;
        }

        int end = (int)node.Span.End;

        while (end < source.Length && source[end] is not (byte)'\n')
        {
            end++;
        }

        if (end < source.Length)
        {
            end++;
        }

        return Result.Success(new Edit(start, end - start, []));
    }

    /// <summary>Écrit un nœud en YAML, réindenté pour l'endroit où il va.</summary>
    /// <remarks>
    /// La première ligne n'est pas réindentée par défaut : elle prend la place d'une valeur dont les
    /// espaces de gauche sont déjà dans les octets d'origine.
    /// </remarks>
    private string Render(
        SpecNode value,
        CanonicalFormatting formatting,
        int indent,
        bool firstLineIndented = false)
    {
        ReadOnlyMemory<byte> emitted = _emitter.Emit(
            value, formatting with { FinalNewline = false, KeyOrder = KeyOrderStyle.Preserve });

        string text = Encoding.UTF8.GetString(emitted.Span).TrimEnd('\n', '\r');
        string[] lines = text.Split('\n');
        string pad = new(' ', indent);
        StringBuilder builder = new();

        for (int at = 0; at < lines.Length; at++)
        {
            if (at > 0)
            {
                builder.Append('\n');
            }

            if (at > 0 || firstLineIndented)
            {
                builder.Append(pad);
            }

            builder.Append(lines[at].TrimEnd('\r'));
        }

        return builder.ToString();
    }



    /// <summary>
    /// La fin réelle du texte qu'un nœud occupe.
    /// </summary>
    /// <remarks>
    /// L'étendue qu'un analyseur YAML donne à une collection de bloc <em>dépasse</em> son contenu : son
    /// repère de fin est posé là où le jeton suivant commence, parce que c'est à cet instant que
    /// l'analyseur constate que la collection est close. L'étendue de <c>paths</c> d'une description du
    /// corpus finissait ainsi après la ligne <c>components:</c>, et une insertion à cet endroit plaçait la
    /// route neuve sous <c>components</c> — un document valide, décrivant autre chose.
    /// <para>
    /// Reculer sur les blancs suffit donc, et vaut pour les deux styles : une collection de flux a un
    /// repère de fin exact, qu'on ne touche pas, tandis qu'une collection de bloc se voit ramenée à son
    /// dernier caractère écrit.
    /// </para>
    /// </remarks>
    private static int ContentEnd(SpecDocument document, SpecNode node)
    {
        ReadOnlySpan<byte> source = document.Source.Span;
        int at = Math.Min((int)node.Span.End, source.Length);
        int floor = (int)node.Span.Start;

        while (at > floor && source[at - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            at--;
        }

        return at;
    }

    /// <summary>Une étendue des octets d'origine, et ce qui la remplace.</summary>
    private sealed record Edit(int Start, int Length, byte[] Replacement);
}
