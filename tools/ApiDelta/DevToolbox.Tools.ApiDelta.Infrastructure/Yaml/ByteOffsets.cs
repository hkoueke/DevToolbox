namespace DevToolbox.Tools.ApiDelta.Infrastructure.Yaml;

/// <summary>
/// Traduit un rang de caractère en décalage d'octet.
/// </summary>
/// <remarks>
/// L'analyseur YAML lit un flux de caractères et situe donc chaque nœud par un rang de caractère.
/// <see cref="DevToolbox.Tools.ApiDelta.Core.Syntax.ByteSpan"/> se dit, lui, en octets — et c'est ce dont
/// l'outil a besoin : réécrire un document, mesurer ce qu'un fragment occupe, découper les octets autour
/// de ce qui change. Confondre les deux ne se voit pas sur un document en ASCII, et décale tout ce qui
/// suit la première lettre accentuée sur un document français.
/// <para>
/// Ce n'était pas théorique : l'étendue du nœud <c>required</c> d'une description du corpus désignait
/// « ed: f » au lieu de « false », parce que quatre caractères sur deux octets la précédaient. L'épreuve
/// de va-et-vient ne pouvait pas le voir, car elle concatène les étendues dans l'ordre du document et les
/// intervalles entre elles absorbent le décalage.
/// </para>
/// </remarks>
internal sealed class ByteOffsets
{
    private readonly int[] _offsets;

    /// <summary>Construit la table pour un texte donné.</summary>
    /// <param name="text">Le texte, tel que l'analyseur le lira.</param>
    internal ByteOffsets(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _offsets = new int[text.Length + 1];

        int bytes = 0;
        int at = 0;

        while (at < text.Length)
        {
            _offsets[at] = bytes;

            if (char.IsHighSurrogate(text[at]) && at + 1 < text.Length
                && char.IsLowSurrogate(text[at + 1]))
            {
                // Une paire de substitution est un seul point de code, écrit sur quatre octets. Le second
                // rang reçoit le même décalage : il ne désigne pas une frontière de caractère, et aucun
                // repère n'a de raison de tomber dessus.
                _offsets[at + 1] = bytes;
                bytes += 4;
                at += 2;

                continue;
            }

            bytes += Width(text[at]);
            at++;
        }

        _offsets[text.Length] = bytes;
    }

    /// <summary>Le nombre total d'octets du texte.</summary>
    internal int ByteLength => _offsets[^1];

    /// <summary>Le décalage d'octet d'un rang de caractère.</summary>
    /// <param name="index">Le rang, tel que l'analyseur le donne.</param>
    /// <returns>Le décalage du premier octet de ce caractère.</returns>
    /// <remarks>
    /// Un rang hors bornes rend la fin du texte plutôt que de lever : un repère de fin de flux peut
    /// désigner la position suivant le dernier caractère, et ce n'est pas une anomalie.
    /// </remarks>
    internal long Of(long index)
    {
        if (index <= 0)
        {
            return 0;
        }

        return index >= _offsets.Length ? _offsets[^1] : _offsets[index];
    }

    /// <summary>Le nombre d'octets qu'un caractère non substitué occupe en UTF-8.</summary>
    private static int Width(char character) => character switch
    {
        < (char)0x80 => 1,
        < (char)0x800 => 2,
        _ => 3,
    };
}
