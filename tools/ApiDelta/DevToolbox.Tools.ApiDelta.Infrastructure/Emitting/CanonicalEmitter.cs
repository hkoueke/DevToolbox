using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;

/// <summary>
/// Écrit un arbre en YAML, sous une forme choisie plutôt que reprise de l'entrée.
/// </summary>
/// <remarks>
/// La quotation est la partie qui compte, et ce n'est pas affaire de style. Un scalaire qui est une chaîne
/// mais qui s'écrirait comme un nombre, un booléen ou une absence de valeur <em>doit</em> être quoté :
/// écrire <c>200</c> là où le document disait <c>'200'</c> changerait un code de réponse en entier, et
/// <c>no</c> à la place de <c>'no'</c> changerait un nom en booléen. L'émetteur regarde donc ce que le
/// scalaire signifie, puis choisit l'écriture qui préserve ce sens.
/// <para>
/// À distinguer de l'émetteur fidèle, qui réassemble les octets d'origine : celui-ci produit une écriture
/// neuve, ce qu'il faut pour un document engendré ou une copie remise en forme, et ce qui explique qu'aucun
/// commentaire n'y survive.
/// </para>
/// </remarks>
public sealed class CanonicalEmitter : ISpecEmitter
{
    private static readonly char[] LeadingIndicators =
        ['-', '?', ':', ',', '[', ']', '{', '}', '#', '&', '*', '!', '|', '>', '\'', '"', '%', '@', '`'];

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Emit(SpecNode root, CanonicalFormatting formatting)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(formatting);

        StringBuilder builder = new();

        WriteNode(builder, root, formatting, depth: 0, inline: false);

        string text = builder.ToString();

        if (formatting.FinalNewline && !text.EndsWith('\n'))
        {
            text += "\n";
        }

        if (formatting.LineEndings == LineEndingStyle.Crlf)
        {
            text = text.ReplaceLineEndings("\r\n");
        }

        return Encoding.UTF8.GetBytes(text);
    }

    private static void WriteNode(
        StringBuilder builder,
        SpecNode node,
        CanonicalFormatting formatting,
        int depth,
        bool inline)
    {
        switch (node)
        {
            case MappingNode mapping:
                WriteMapping(builder, mapping, formatting, depth, inline);
                break;

            case SequenceNode sequence:
                WriteSequence(builder, sequence, formatting, depth, inline);
                break;

            case ScalarNode scalar:
                builder.Append(inline ? string.Empty : Indent(formatting, depth))
                    .AppendLine(Single(scalar));
                break;
        }
    }

    private static void WriteMapping(
        StringBuilder builder,
        MappingNode mapping,
        CanonicalFormatting formatting,
        int depth,
        bool inline)
    {
        if (mapping.Entries.Count == 0)
        {
            builder.Append(inline ? string.Empty : Indent(formatting, depth)).AppendLine("{}");
            return;
        }

        bool first = true;

        foreach (MappingEntry entry in Order(mapping, formatting))
        {
            if (!first || !inline)
            {
                builder.Append(Indent(formatting, depth));
            }

            first = false;

            WriteEntry(builder, entry, formatting, depth);
        }
    }

    private static void WriteEntry(
        StringBuilder builder,
        MappingEntry entry,
        CanonicalFormatting formatting,
        int depth)
    {
        builder.Append(Key(entry.Key)).Append(':');

        if (entry.Value is ScalarNode scalar)
        {
            builder.Append(' ').AppendLine(Single(scalar));
            return;
        }

        if (IsEmptyCollection(entry.Value))
        {
            builder.Append(' ').AppendLine(entry.Value is MappingNode ? "{}" : "[]");
            return;
        }

        builder.AppendLine();
        WriteNode(builder, entry.Value, formatting, depth + 1, inline: false);
    }

    private static void WriteSequence(
        StringBuilder builder,
        SequenceNode sequence,
        CanonicalFormatting formatting,
        int depth,
        bool inline)
    {
        if (sequence.Items.Count == 0)
        {
            builder.Append(inline ? string.Empty : Indent(formatting, depth)).AppendLine("[]");
            return;
        }

        bool first = true;

        foreach (SpecNode item in sequence.Items)
        {
            if (!first || !inline)
            {
                builder.Append(Indent(formatting, depth));
            }

            first = false;

            builder.Append("- ");

            if (item is ScalarNode scalar)
            {
                builder.AppendLine(Single(scalar));
                continue;
            }

            if (IsEmptyCollection(item))
            {
                builder.AppendLine(item is MappingNode ? "{}" : "[]");
                continue;
            }

            // Un membre composite commence sur la ligne du tiret puis s'indente d'un niveau : c'est la
            // forme qu'un lecteur attend d'une liste de paramètres ou de serveurs.
            WriteNode(builder, item, formatting, depth + 1, inline: true);
        }
    }

    /// <summary>
    /// L'ordre d'écriture des clés : celui du document, ou un ordre stable.
    /// </summary>
    /// <remarks>
    /// L'ordre canonique trie par clé, ce qui donne un fichier qui ne bouge plus d'une exécution à
    /// l'autre. Conserver l'ordre d'origine reste proposé, parce qu'un document relu par un humain gagne
    /// à ressembler au document dont il provient.
    /// </remarks>
    private static IEnumerable<MappingEntry> Order(
        MappingNode mapping,
        CanonicalFormatting formatting) =>
        formatting.KeyOrder == KeyOrderStyle.Preserve
            ? mapping.Entries
            : mapping.Entries.OrderBy(entry => entry.Key.Text, StringComparer.Ordinal);

    private static bool IsEmptyCollection(SpecNode node) => node switch
    {
        MappingNode mapping => mapping.Entries.Count == 0,
        SequenceNode sequence => sequence.Items.Count == 0,
        _ => false,
    };

    private static string Indent(CanonicalFormatting formatting, int depth) =>
        new(' ', depth * formatting.Indent);

    private static string Key(ScalarNode key) => Single(key);

    /// <summary>Écrit un scalaire sur une seule ligne, sous la forme qui préserve son sens.</summary>
    private static string Single(ScalarNode scalar)
    {
        ResolvedScalar resolved = scalar.Resolved;

        if (resolved.Kind is ScalarValueKind.Boolean or ScalarValueKind.Integral
            or ScalarValueKind.Number)
        {
            // La forme canonique, non l'écriture d'origine : « 1.0 » et « 1 » sont le même nombre, et la
            // sortie en choisit une.
            return resolved.Canonical;
        }

        return resolved.Kind == ScalarValueKind.Null ? "null" : Plain(scalar.Text);
    }

    /// <summary>Écrit un texte, quoté si l'écrire sans guillemets changerait ce qu'il est.</summary>
    private static string Plain(string text)
    {
        if (text.Contains('\n', StringComparison.Ordinal))
        {
            // Un texte multiligne est écrit entre guillemets avec ses échappements, non en bloc littéral.
            // Le bloc est plus agréable à lire, et son indicateur de conservation est aussi un piège : se
            // tromper entre « | » et « |- » ajoute ou retire un caractère au contenu. La forme échappée
            // est toujours exacte ; le bloc viendra avec l'émission de la copie optimisée, sous un essai
            // de propriété qui le prouve.
            return Escaped(text);
        }

        return NeedsQuotes(text)
            ? "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'"
            : text;
    }

    /// <summary>
    /// Établit si un texte doit être quoté pour rester le texte qu'il est.
    /// </summary>
    /// <remarks>
    /// Le cas qui justifie à lui seul cette méthode : <c>'200'</c>. Sans guillemets, c'est l'entier 200, et
    /// un code de réponse cesse d'être une clé de réponse. Même raison pour <c>'no'</c>, <c>'null'</c>,
    /// <c>'1.5'</c> et <c>'2026-10-17'</c>.
    /// </remarks>
    private static bool NeedsQuotes(string text)
    {
        if (text.Length == 0
            || char.IsWhiteSpace(text[0])
            || char.IsWhiteSpace(text[^1])
            || LeadingIndicators.Contains(text[0]))
        {
            return true;
        }

        if (text.Contains(": ", StringComparison.Ordinal)
            || text.Contains(" #", StringComparison.Ordinal)
            || text.EndsWith(':'))
        {
            return true;
        }

        // Le texte est une chaîne : écrit sans guillemets, se relirait-il comme autre chose ?
        return CoreSchemaResolver.ResolvePlain(text).Kind != ScalarValueKind.Text;
    }

    private static string Escaped(string text)
    {
        StringBuilder builder = new("\"");

        foreach (char character in text)
        {
            builder.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => character.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }
}
