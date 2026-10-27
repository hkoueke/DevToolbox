using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Convertit un brouillon en arbre de syntaxe, sans aucune étendue d'octets.</summary>
/// <remarks>
/// Sans étendue, et c'est l'invariant de toute la projection. L'épissage se repère aux étendues pour
/// recopier les octets d'origine autour de ce qui change ; un nœud projeté ne vient d'aucun octet, et une
/// étendue inventée le ferait écrire n'importe où. <see cref="ByteSpan.None"/> est posé partout, et un
/// essai le vérifie sur l'arbre entier.
/// <para>
/// Les positions valent zéro pour la même raison. Un nœud projeté n'est à aucune ligne d'aucun fichier,
/// et prétendre le contraire enverrait un développeur chercher la ligne 14 d'un document qui n'existe pas.
/// </para>
/// </remarks>
public static class DraftWriter
{
    private static readonly SourcePosition Nowhere = new(0, 0);

    /// <summary>Écrit un brouillon sous la forme d'un nœud.</summary>
    /// <param name="draft">Le brouillon.</param>
    /// <param name="at">Où le nœud se situe dans le document.</param>
    /// <returns>Le nœud.</returns>
    public static SpecNode Write(SchemaDraft draft, SpecPointer at)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(at);

        List<MappingEntry> entries = [];

        foreach (KeyValuePair<string, object?> keyword in draft.Keywords)
        {
            SpecPointer child = at.Child(keyword.Key);

            entries.Add(new MappingEntry(Text(keyword.Key, child), Value(keyword.Value, child)));
        }

        return new MappingNode(at, Nowhere, ByteSpan.None, entries);
    }

    /// <summary>Écrit une valeur de brouillon : un sous-brouillon, une liste, ou un scalaire.</summary>
    private static SpecNode Value(object? value, SpecPointer at) => value switch
    {
        SchemaDraft nested => Write(nested, at),
        List<object?> items => Items(items, at),
        null => Scalar("null", ScalarValueKind.Null, at),
        bool flag => Scalar(flag ? "true" : "false", ScalarValueKind.Boolean, at),
        long number => Scalar(Invariant(number), ScalarValueKind.Integral, at),
        int number => Scalar(Invariant(number), ScalarValueKind.Integral, at),
        decimal number => Scalar(Invariant(number), ScalarValueKind.Number, at),
        double number => Scalar(Invariant(number), ScalarValueKind.Number, at),
        _ => Text(value.ToString() ?? string.Empty, at),
    };

    private static SequenceNode Items(List<object?> items, SpecPointer at)
    {
        List<SpecNode> written = [];

        for (int index = 0; index < items.Count; index++)
        {
            written.Add(Value(items[index], at.Item(index)));
        }

        return new SequenceNode(at, Nowhere, ByteSpan.None, written);
    }

    /// <summary>Un scalaire textuel.</summary>
    /// <param name="text">Son texte.</param>
    /// <param name="at">Où il se situe.</param>
    /// <returns>Le nœud.</returns>
    public static ScalarNode Text(string text, SpecPointer at)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(at);

        return new ScalarNode(
            at, Nowhere, ByteSpan.None, text, Style(text), ResolvedScalar.FromString(text));
    }

    /// <summary>
    /// Le style d'écriture qu'un texte exige.
    /// </summary>
    /// <remarks>
    /// Un « $ref » vaut « #/components/schemas/X », et un dièse en tête de scalaire ouvre un commentaire
    /// en YAML : écrit en style simple, ce renvoi disparaîtrait. L'arbre doit être fidèle au sens
    /// <em>et</em> à l'écriture, y compris quand il est projeté et que rien ne le réémet aujourd'hui —
    /// un arbre dont l'écriture est fausse attend seulement le premier émetteur qu'on y branchera.
    /// </remarks>
    private static ScalarStyle Style(string text) =>
        text.Length > 0 && (text[0] is '#' or '*' or '&' or '!' or '%' or '@' or '`'
            || text.Contains(": ", StringComparison.Ordinal))
            ? ScalarStyle.DoubleQuoted
            : ScalarStyle.Plain;

    private static ScalarNode Scalar(string text, ScalarValueKind kind, SpecPointer at) =>
        new(
            at,
            Nowhere,
            ByteSpan.None,
            text,
            ScalarStyle.Plain,
            new ResolvedScalar(kind, text));

    private static string Invariant(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}
