namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Ce que la projection a produit, en chiffres.</summary>
/// <param name="Operations">Le nombre d'opérations projetées.</param>
/// <param name="Schemas">Le nombre de schémas produits.</param>
/// <param name="Carried">Le nombre de constructions portées.</param>
/// <param name="Approximate">Le nombre de constructions portées à peu près.</param>
/// <param name="NotCarried">Le nombre de constructions non portées.</param>
/// <param name="DeclinedTypes">Le nombre de types déclarés non projetables.</param>
/// <remarks>
/// Un chiffre qu'on peut recouper. Un rapport qui dirait « la projection a réussi » sans dire combien de
/// constructions elle a laissées derrière laisserait croire à une équivalence, ce qui est exactement ce
/// que cette fonctionnalité ne promet pas.
/// </remarks>
public sealed record ProjectionFigures(
    int Operations,
    int Schemas,
    int Carried,
    int Approximate,
    int NotCarried,
    int DeclinedTypes)
{
    /// <summary>Indique que tout ce que le contrat portait a été porté.</summary>
    public bool IsFaithful => Approximate == 0 && NotCarried == 0 && DeclinedTypes == 0;
}
