using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un attribut XSD n'a aucune contrepartie. La plus grosse source d'écarts en pratique.</summary>
/// <remarks>
/// En pratique, et le chiffre compte : un type métier porte couramment des dizaines d'attributs
/// techniques — version, verrouillage, identifiant interne — qu'un document OpenAPI ne sait pas
/// distinguer d'une propriété. Les porter comme des propriétés serait affirmer une équivalence fausse ;
/// les taire laisserait croire que le contrat ne dit rien d'eux.
/// </remarks>
public sealed class AttributeRule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public AttributeRule()
        : base("`xsd:attribute`", ProjectionGapReason.Attribute)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) => subject?.Attribute is not null;

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "attribut « " + subject.Attribute!.Name + " »";
}
