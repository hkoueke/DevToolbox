using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>L'espace de noms d'une déclaration n'a pas de contrepartie : JSON n'en a pas.</summary>
/// <remarks>
/// Deux types homonymes dans deux espaces de noms sont deux types ; projetés, ils portent le même nom
/// local. La projection les distingue par leur nom qualifié, mais le contrat produit ne dit plus
/// <em>pourquoi</em> ils sont distincts, et un lecteur y verra une redondance à nettoyer.
/// </remarks>
public sealed class NamespaceRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public NamespaceRule()
        : base("namespace of a declaration", ProjectionGapReason.Namespace)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { } type && type.Name.Contains(':', StringComparison.Ordinal);

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "espace de noms de « " + subject.Type!.Name + " »";
}
