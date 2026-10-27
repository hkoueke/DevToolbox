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

    /// <summary>L'absence d'étendue : un nœud qui ne vient d'aucun octet.</summary>
    /// <remarks>
    /// Nommée plutôt que laissée à « new ByteSpan(0, 0) ». Un nœud projeté depuis un WSDL ne provient
    /// d'aucun octet d'un document OpenAPI, et l'épissage se repère aux étendues : écrire l'absence
    /// permet de la reconnaître, et à un essai de vérifier qu'elle est partout.
    /// </remarks>
    public static ByteSpan None { get; } = new(0, 0);
}
