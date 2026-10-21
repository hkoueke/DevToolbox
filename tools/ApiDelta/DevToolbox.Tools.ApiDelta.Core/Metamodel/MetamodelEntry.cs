using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>Ce que le métamodèle sait d'un emplacement.</summary>
/// <param name="Kind">La sorte d'objet auquel l'emplacement appartient.</param>
/// <param name="Role">Ce que le champ engage.</param>
/// <param name="Collection">Ce qu'est la collection, le cas échéant.</param>
/// <param name="Identity">Comment ses membres se reconnaissent.</param>
/// <param name="WhenAdded">L'effet d'une apparition.</param>
/// <param name="WhenRemoved">L'effet d'une disparition.</param>
/// <param name="WhenModified">L'effet d'une modification, quand la valeur n'entre pas en jeu.</param>
/// <param name="ValueRule">La règle sensible à la valeur, quand il y en a une.</param>
/// <param name="Policy">La clé de politique, quand l'effet est un choix et non une règle.</param>
/// <param name="ChildKind">La sorte des enfants, quand elle est déterminée.</param>
public sealed record MetamodelEntry(
    SpecObjectKind Kind,
    FieldRole Role,
    CollectionKind Collection = CollectionKind.None,
    IdentityRule Identity = IdentityRule.ByKey,
    ChangeImpact WhenAdded = ChangeImpact.Unknown,
    ChangeImpact WhenRemoved = ChangeImpact.Unknown,
    ChangeImpact WhenModified = ChangeImpact.Unknown,
    ValueAwareRule ValueRule = ValueAwareRule.None,
    string? Policy = null,
    SpecObjectKind ChildKind = SpecObjectKind.Unknown)
{
    /// <summary>
    /// L'entrée que reçoit un emplacement que la table ne décrit pas.
    /// </summary>
    /// <remarks>
    /// Le défaut est sûr dans le seul sens qui compte : comparé comme une association sans ordre, tenu pour
    /// susceptible d'agir sur le comportement, classé inconnu, et jamais éligible à une transformation
    /// automatique. Une ligne manquante rend donc l'outil moins décidé, jamais faux.
    /// </remarks>
    public static MetamodelEntry SafeDefault { get; } = new(
        SpecObjectKind.Unknown,
        FieldRole.Vendor,
        WhenAdded: ChangeImpact.Unknown,
        WhenRemoved: ChangeImpact.Unknown,
        WhenModified: ChangeImpact.Unknown);

    /// <summary>Indique si l'emplacement relève de la documentation seule.</summary>
    public bool IsDocumentation => Role == FieldRole.Documentation;
}
