namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Ce que la projection d'une construction XSD vers OpenAPI vaut.</summary>
/// <remarks>
/// Trois membres, et c'est le troisième qui rend la fonctionnalité honnête. Sans lui, une correspondance
/// qui tient à peu près serait annoncée comme une équivalence, et un lecteur ne peut pas récupérer le
/// doute depuis un constat assuré.
/// </remarks>
public enum ProjectionConfidence
{
    /// <summary>La correspondance tient. Un constat porte la confiance du comparateur lui-même.</summary>
    Carried,

    /// <summary>
    /// La correspondance tient à peu près. Un constat le dit et porte une confiance incertaine.
    /// </summary>
    Approximate,

    /// <summary>
    /// Il n'y a pas de contrepartie. Produit un écart groupé par motif, jamais un constat par occurrence.
    /// </summary>
    NotCarried,
}
