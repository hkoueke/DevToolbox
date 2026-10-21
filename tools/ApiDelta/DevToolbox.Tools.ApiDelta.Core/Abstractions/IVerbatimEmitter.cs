namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Réécrit un document non modifié, à l'identique.</summary>
/// <remarks>
/// Son intérêt est la vérification : un arbre qui se réécrit octet pour octet est un arbre qui couvre
/// réellement le document. L'émission canonique — indentation, quotation, ordre des clés choisis — est un
/// autre port, parce que c'en est un autre métier.
/// </remarks>
public interface IVerbatimEmitter
{
    /// <summary>Réassemble le document à partir des étendues de son arbre.</summary>
    /// <param name="document">Le document lu.</param>
    /// <returns>Les octets réassemblés.</returns>
    ReadOnlyMemory<byte> Emit(SpecDocument document);
}
