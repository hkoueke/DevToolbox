namespace DevToolbox.Tools.ApiDelta.Infrastructure.Json;

/// <summary>
/// Traduit un décalage d'octet en ligne et colonne.
/// </summary>
/// <remarks>
/// Le lecteur JSON de la plateforme donne des décalages, pas des positions. Un développeur, lui, lit des
/// lignes : sans cette traduction, une erreur dans un fichier de quinze mille lignes serait signalée « à
/// l'octet 412 988 », ce qui ne se cherche pas dans un éditeur.
/// <para>
/// L'index est construit une fois par document, puis consulté par recherche dichotomique.
/// </para>
/// </remarks>
internal sealed class LineIndex
{
    private readonly int[] _lineStarts;

    internal LineIndex(ReadOnlySpan<byte> content)
    {
        List<int> starts = [0];

        for (int index = 0; index < content.Length; index++)
        {
            if (content[index] == (byte)'\n')
            {
                starts.Add(index + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    /// <summary>La position correspondant à un décalage d'octet.</summary>
    /// <param name="offset">Le décalage, à partir de zéro.</param>
    /// <returns>La ligne et la colonne, à partir de un.</returns>
    internal Core.Syntax.SourcePosition At(long offset)
    {
        int target = offset < 0 ? 0 : (int)Math.Min(offset, int.MaxValue);

        int line = Array.BinarySearch(_lineStarts, target);

        if (line < 0)
        {
            // Pas un début de ligne : l'insertion désigne la ligne suivante, donc la précédente contient
            // le décalage.
            line = ~line - 1;
        }

        return new Core.Syntax.SourcePosition(line + 1, target - _lineStarts[line] + 1);
    }
}
