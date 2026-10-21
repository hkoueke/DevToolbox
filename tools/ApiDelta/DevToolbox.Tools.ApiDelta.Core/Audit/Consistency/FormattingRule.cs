using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les écarts à la mise en forme convenue.
/// </summary>
/// <remarks>
/// La règle lit les octets d'origine, et non l'arbre : l'indentation, les espaces en fin de ligne et la
/// fin de ligne finale ne survivent pas à une représentation sémantique, et ce sont justement les écarts
/// qui font qu'un diff de production est illisible. C'est le seul endroit de l'audit où le texte compte
/// plus que le sens.
/// </remarks>
public sealed class FormattingRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public FormattingRule()
        : base("AUD-200", SpecObjectKind.Document)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.Document.Format != SpecFormat.Yaml)
        {
            return RuleReport.NotEvaluated(
                "La mise en forme convenue décrit du YAML ; cette description est écrite en JSON.");
        }

        string text = Encoding.UTF8.GetString(subject.Document.Source.Span);
        CanonicalFormatting wanted = subject.Ruleset.Formatting;

        return RuleReport.Evaluated(
        [
            .. Endings(text, wanted),
            .. FinalNewline(text, wanted),
            .. Trailing(text),
            .. TooWide(text, wanted),
            .. Indentation(text, wanted),
        ]);
    }

    private IEnumerable<Finding> Endings(string text, CanonicalFormatting wanted)
    {
        bool hasCrLf = text.Contains("\r\n", StringComparison.Ordinal);
        bool hasLf = text.Replace("\r\n", string.Empty, StringComparison.Ordinal)
            .Contains('\n', StringComparison.Ordinal);

        if (hasCrLf && hasLf)
        {
            yield return Line("Le fichier mêle des fins de ligne CRLF et LF.", 1);
            yield break;
        }

        bool wantsCrLf = wanted.LineEndings == LineEndingStyle.Crlf;

        if (hasCrLf != wantsCrLf && (hasCrLf || hasLf))
        {
            yield return Line(
                $"Les fins de ligne sont {(hasCrLf ? "CRLF" : "LF")} là où {(wantsCrLf ? "CRLF" : "LF")} "
                    + "est convenu.",
                1);
        }
    }

    private IEnumerable<Finding> FinalNewline(string text, CanonicalFormatting wanted)
    {
        bool ends = text.EndsWith('\n');

        if (wanted.FinalNewline && !ends)
        {
            yield return Line("Le fichier ne se termine pas par une fin de ligne.", Count(text));
        }
        else if (!wanted.FinalNewline && ends)
        {
            yield return Line("Le fichier se termine par une fin de ligne, qui n'est pas convenue.", Count(text));
        }
    }

    private IEnumerable<Finding> Trailing(string text) =>
        Lines(text)
            .Where(line => line.Text.Length > 0 && char.IsWhiteSpace(line.Text[^1]))
            .Select(line => Line("La ligne se termine par des espaces.", line.Number));

    private IEnumerable<Finding> TooWide(string text, CanonicalFormatting wanted) =>
        Lines(text)
            .Where(line => line.Text.Length > wanted.LineWidth)
            .Select(line => Line(
                $"La ligne fait {line.Text.Length} caractères, au-delà des {wanted.LineWidth} convenus.",
                line.Number));

    /// <summary>
    /// Les lignes dont l'indentation n'est pas un multiple du pas convenu.
    /// </summary>
    /// <remarks>
    /// Un multiple, et non une valeur attendue : l'indentation juste d'une ligne dépend de sa profondeur
    /// dans l'arbre, que le texte seul ne donne pas. Un multiple se vérifie sans se tromper.
    /// </remarks>
    private IEnumerable<Finding> Indentation(string text, CanonicalFormatting wanted) =>
        Lines(text)
            .Where(line => line.Text.Trim().Length > 0)
            .Where(line => Indent(line.Text) % wanted.Indent != 0)
            .Select(line => Line(
                $"La ligne est indentée de {Indent(line.Text)} espaces, qui n'est pas un multiple de "
                    + $"{wanted.Indent}.",
                line.Number));

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    private static int Count(string text) => Lines(text).Count;

    private static IReadOnlyList<(int Number, string Text)> Lines(string text) =>
    [
        .. text.ReplaceLineEndings("\n").Split('\n')
            .Select((line, index) => (Number: index + 1, Text: line)),
    ];

    /// <summary>
    /// Un constat de mise en forme, situé par une ligne.
    /// </summary>
    /// <remarks>
    /// L'emplacement est la racine, et la ligne est dans le message : un écart de mise en forme n'a pas de
    /// pointeur, et en fabriquer un qui ressemble à un pointeur ferait croire à un emplacement dans la
    /// structure du document.
    /// </remarks>
    private Finding Line(string message, int number) =>
        Report(
            "Ligne " + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " : " + message,
            SpecPointer.Root,
            "Réécrire la description sous la mise en forme convenue.");
}
