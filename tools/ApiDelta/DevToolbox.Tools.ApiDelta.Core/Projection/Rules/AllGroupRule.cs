using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un « xsd:all » devient des propriétés d'objet, l'ordre étant ignoré.</summary>
/// <remarks>
/// La seule ligne de la table où la correspondance est <em>exacte</em> plutôt que fidèle : les membres
/// d'un objet JSON sont non ordonnés, et « all » dit précisément que l'ordre ne compte pas. Là où une
/// séquence perd son exigence d'ordre en se projetant, un « all » n'en avait aucune à perdre.
/// </remarks>
public sealed class AllGroupRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public AllGroupRule()
        : base("`xsd:all`", "object properties, order ignored", ProjectionConfidence.Carried)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { Content: Wsdl.XsdContentKind.All };

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        into.Set("type", "object");

        foreach (Wsdl.XsdParticle particle in subject.Type!.Particles)
        {
            _ = into.Property(particle.Name);
        }
    }
}
