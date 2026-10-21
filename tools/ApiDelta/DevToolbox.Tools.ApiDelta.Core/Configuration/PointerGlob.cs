namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>
/// Apparie un pointeur JSON à un motif : <c>*</c> couvre un segment, <c>**</c> n'importe quel nombre.
/// </summary>
/// <remarks>
/// Segment par segment, et non par expression régulière sur la chaîne entière. La différence compte :
/// un chemin OpenAPI contient des barres obliques échappées en <c>~1</c>, et une expression régulière
/// gourmande sur la chaîne ferait d'un <c>*</c> le franchissement de frontières de segments que l'auteur
/// du motif ne voulait pas franchir. C'est exactement pour cela que <c>**</c> existe et se demande.
/// </remarks>
public static class PointerGlob
{
    /// <summary>Indique si un pointeur est apparié par un motif.</summary>
    /// <param name="pattern">Le motif, sous la forme <c>/paths/*</c> ou <c>/paths/**/get</c>.</param>
    /// <param name="target">Le pointeur à situer.</param>
    /// <returns><see langword="true"/> si le motif couvre ce pointeur.</returns>
    public static bool Matches(string pattern, string target)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(target);

        if (string.Equals(pattern, target, StringComparison.Ordinal))
        {
            return true;
        }

        return Walk(Segments(pattern), 0, Segments(target), 0);
    }

    /// <summary>Les segments d'un pointeur, l'échappement laissé tel quel.</summary>
    /// <remarks>
    /// Laissé tel quel parce que le motif est écrit dans la même forme que le pointeur qu'un rapport
    /// affiche : un auteur de sourdine recopie ce qu'il a lu, et il a lu <c>/paths/~1documents/post</c>.
    /// </remarks>
    private static string[] Segments(string path) =>
        path.Trim('/') is { Length: > 0 } trimmed ? trimmed.Split('/') : [];

    /// <summary>
    /// Avance dans le motif et dans le pointeur en même temps, en sauvegardant un retour sur
    /// <c>**</c>.
    /// </summary>
    private static bool Walk(
        IReadOnlyList<string> pattern,
        int atPattern,
        IReadOnlyList<string> pointer,
        int atPointer)
    {
        while (atPattern < pattern.Count)
        {
            if (string.Equals(pattern[atPattern], "**", StringComparison.Ordinal))
            {
                // Zéro segment, puis un, puis deux : la première longueur qui apparie gagne.
                for (int taken = 0; atPointer + taken <= pointer.Count; taken++)
                {
                    if (Walk(pattern, atPattern + 1, pointer, atPointer + taken))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (atPointer >= pointer.Count)
            {
                return false;
            }

            if (!string.Equals(pattern[atPattern], "*", StringComparison.Ordinal)
                && !string.Equals(pattern[atPattern], pointer[atPointer], StringComparison.Ordinal))
            {
                return false;
            }

            atPattern++;
            atPointer++;
        }

        return atPointer == pointer.Count;
    }
}
