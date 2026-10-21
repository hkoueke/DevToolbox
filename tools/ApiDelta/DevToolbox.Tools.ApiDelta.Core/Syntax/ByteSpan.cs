namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>L'étendue qu'une chose occupe dans les octets d'origine.</summary>
/// <param name="Start">Le décalage du premier octet.</param>
/// <param name="Length">Le nombre d'octets.</param>
/// <remarks>
/// C'est ce qui rend les économies mesurables plutôt qu'estimées à vue : la taille d'un fragment est le
/// nombre d'octets qu'il occupe réellement, non une approximation déduite de sa forme.
/// </remarks>
public sealed record ByteSpan(long Start, long Length)
{
    /// <summary>Le décalage du premier octet situé après l'étendue.</summary>
    public long End => Start + Length;
}
