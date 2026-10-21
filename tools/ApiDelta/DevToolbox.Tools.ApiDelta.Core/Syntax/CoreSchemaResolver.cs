using System.Globalization;

namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>
/// Résout un scalaire selon le schéma de base de YAML 1.2, et signale ce qui n'est pas univoque.
/// </summary>
/// <remarks>
/// Le schéma de base de YAML 1.2 est strict : <c>true</c> et <c>false</c> sont des booléens, <c>yes</c> et
/// <c>no</c> sont des chaînes. YAML 1.1 en faisait des booléens, et beaucoup d'outils — comme beaucoup
/// d'auteurs — le croient encore. Une écriture dont les deux lectures diffèrent est donc résolue selon
/// 1.2 <em>et</em> signalée : trancher en silence, c'est décider à la place de l'auteur ce que dit son
/// contrat.
/// <para>
/// L'ambiguïté ne concerne que les scalaires sans guillemets. <c>'no'</c> est une chaîne sans l'ombre d'un
/// doute, et n'est jamais signalé.
/// </para>
/// </remarks>
public static class CoreSchemaResolver
{
    private static readonly string[] NullForms = ["", "~", "null", "Null", "NULL"];

    private static readonly string[] TrueForms = ["true", "True", "TRUE"];

    private static readonly string[] FalseForms = ["false", "False", "FALSE"];

    /// <summary>
    /// Les écritures que YAML 1.1 lisait comme des booléens et que 1.2 lit comme des chaînes.
    /// </summary>
    private static readonly string[] LegacyBooleans =
    [
        "y", "Y", "yes", "Yes", "YES", "n", "N", "no", "No", "NO",
        "on", "On", "ON", "off", "Off", "OFF",
    ];

    /// <summary>Résout un scalaire écrit sans guillemets.</summary>
    /// <param name="text">Le texte, tel qu'il était écrit.</param>
    /// <returns>Ce que la valeur signifie, et le cas échéant pourquoi ce n'est pas univoque.</returns>
    public static ResolvedScalar ResolvePlain(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (LegacyBooleans.Contains(text, StringComparer.Ordinal))
        {
            return ResolvedScalar.Ambiguous(
                text,
                $"« {text} » est une chaîne en YAML 1.2 et un booléen en YAML 1.1 ; "
                    + "entourez-la de guillemets pour lever le doute");
        }

        if (NullForms.Contains(text, StringComparer.Ordinal))
        {
            return new ResolvedScalar(ScalarValueKind.Null, "null");
        }

        if (TrueForms.Contains(text, StringComparer.Ordinal))
        {
            return new ResolvedScalar(ScalarValueKind.Boolean, "true");
        }

        if (FalseForms.Contains(text, StringComparer.Ordinal))
        {
            return new ResolvedScalar(ScalarValueKind.Boolean, "false");
        }

        if (LooksLikeDate(text))
        {
            return ResolvedScalar.Ambiguous(
                text,
                "cette écriture est une chaîne en YAML 1.2 et une date pour d'autres lecteurs ; "
                    + "entourez-la de guillemets pour lever le doute");
        }

        return ResolveNumber(text) ?? ResolvedScalar.FromString(text);
    }

    /// <summary>Résout un scalaire écrit entre guillemets : c'est une chaîne, sans question.</summary>
    /// <param name="text">Le texte, guillemets retirés.</param>
    /// <returns>La valeur résolue.</returns>
    public static ResolvedScalar ResolveQuoted(string text) => ResolvedScalar.FromString(text);

    /// <summary>
    /// Met un nombre sous forme canonique, afin que deux écritures d'une même valeur partagent une
    /// empreinte.
    /// </summary>
    /// <remarks>
    /// <c>maximum: 1.0</c> et <c>maximum: 1</c> sont la même contrainte, et doivent donc avoir la même
    /// empreinte. L'arbre se souvient par ailleurs des deux écritures, de sorte que l'audit de mise en
    /// forme puisse les distinguer.
    /// </remarks>
    private static ResolvedScalar? ResolveNumber(string text)
    {
        if (TryResolveSpecialFloat(text, out ResolvedScalar? special))
        {
            return special;
        }

        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole)
            && !HasLeadingZero(text))
        {
            return new ResolvedScalar(
                ScalarValueKind.Integral, whole.ToString(CultureInfo.InvariantCulture));
        }

        if (HasLeadingZero(text) && long.TryParse(
                text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            return ResolvedScalar.Ambiguous(
                text,
                "un zéro initial fait de cette écriture une chaîne en YAML 1.2 et un nombre octal pour "
                    + "d'autres lecteurs ; entourez-la de guillemets pour lever le doute");
        }

        if (TryResolveRadix(text, out ResolvedScalar? radix))
        {
            return radix;
        }

        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double fractional))
        {
            return new ResolvedScalar(ScalarValueKind.Number, Canonicalize(fractional));
        }

        return null;
    }

    private static bool TryResolveSpecialFloat(string text, out ResolvedScalar? resolved)
    {
        resolved = text switch
        {
            ".inf" or ".Inf" or ".INF" or "+.inf" or "+.Inf" or "+.INF" =>
                new ResolvedScalar(ScalarValueKind.Number, "Infinity"),
            "-.inf" or "-.Inf" or "-.INF" => new ResolvedScalar(ScalarValueKind.Number, "-Infinity"),
            ".nan" or ".NaN" or ".NAN" => new ResolvedScalar(ScalarValueKind.Number, "NaN"),
            _ => null,
        };

        return resolved is not null;
    }

    private static bool TryResolveRadix(string text, out ResolvedScalar? resolved)
    {
        resolved = null;

        if (text.StartsWith("0x", StringComparison.Ordinal)
            && long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
        {
            resolved = new ResolvedScalar(
                ScalarValueKind.Integral, hex.ToString(CultureInfo.InvariantCulture));
        }
        else if (text.StartsWith("0o", StringComparison.Ordinal) && IsOctal(text[2..]))
        {
            resolved = new ResolvedScalar(
                ScalarValueKind.Integral,
                Convert.ToInt64(text[2..], 8).ToString(CultureInfo.InvariantCulture));
        }

        return resolved is not null;
    }

    /// <summary>Rend un nombre à virgule sous une forme stable, entière quand il l'est.</summary>
    private static string Canonicalize(double value)
    {
        if (double.IsInteger(value) && Math.Abs(value) < 1e15)
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static bool HasLeadingZero(string text)
    {
        string digits = text.TrimStart('+', '-');

        return digits.Length > 1 && digits[0] == '0';
    }

    private static bool IsOctal(string text) =>
        text.Length > 0 && text.All(character => character is >= '0' and <= '7');

    /// <summary>
    /// Reconnaît une date non quotée. Volontairement sommaire : il s'agit de signaler le doute, non de
    /// valider une date.
    /// </summary>
    private static bool LooksLikeDate(string text) =>
        text.Length >= 8
        && text.Length <= 10
        && text.Count(character => character == '-') == 2
        && text.All(character => char.IsAsciiDigit(character) || character == '-');
}
