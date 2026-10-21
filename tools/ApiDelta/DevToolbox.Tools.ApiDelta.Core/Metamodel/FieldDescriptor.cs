using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>Une ligne de la table : ce qu'un champ nommé est, dans une sorte d'objet donnée.</summary>
/// <param name="Role">Ce que le champ engage.</param>
/// <param name="Child">La sorte de ce qu'il contient.</param>
/// <param name="Collection">Ce qu'est la collection, le cas échéant.</param>
/// <param name="Identity">Comment ses membres se reconnaissent.</param>
/// <param name="WhenAdded">L'effet d'une apparition.</param>
/// <param name="WhenRemoved">L'effet d'une disparition.</param>
/// <param name="WhenModified">L'effet d'une modification.</param>
/// <param name="ValueRule">La règle sensible à la valeur, quand il y en a une.</param>
/// <param name="Policy">La clé de politique, quand l'effet est un choix.</param>
/// <remarks>
/// Les valeurs par défaut disent le cas le plus fréquent : un champ de contrat dont l'apparition est
/// compatible, la disparition cassante, et la modification inconnue faute de mieux. Chaque ligne ne
/// précise donc que ce qui s'en écarte, ce qui rend la table lisible — et une ligne lisible est une ligne
/// qu'un relecteur peut contredire.
/// </remarks>
public sealed record FieldDescriptor(
    FieldRole Role = FieldRole.Contract,
    SpecObjectKind Child = SpecObjectKind.Unknown,
    CollectionKind Collection = CollectionKind.None,
    IdentityRule Identity = IdentityRule.ByKey,
    ChangeImpact WhenAdded = ChangeImpact.NonBreaking,
    ChangeImpact WhenRemoved = ChangeImpact.Breaking,
    ChangeImpact WhenModified = ChangeImpact.Unknown,
    ValueAwareRule ValueRule = ValueAwareRule.None,
    string? Policy = null)
{
    /// <summary>De la documentation : son apparition, sa disparition et sa modification se lisent seules.</summary>
    public static FieldDescriptor Documentation { get; } = new(
        FieldRole.Documentation,
        WhenAdded: ChangeImpact.DocumentationOnly,
        WhenRemoved: ChangeImpact.DocumentationOnly,
        WhenModified: ChangeImpact.DocumentationOnly);

    /// <summary>Un champ de contrat ordinaire, dont toute modification rompt.</summary>
    public static FieldDescriptor Contractual { get; } = new(
        WhenAdded: ChangeImpact.Breaking,
        WhenRemoved: ChangeImpact.Breaking,
        WhenModified: ChangeImpact.Breaking);
}
