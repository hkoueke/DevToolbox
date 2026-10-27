using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un « minOccurs="0" » laisse la propriété hors de « required ».</summary>
/// <remarks>
/// L'absence de la liste, et non un « required: false » — qui n'existe pas en JSON Schema. La
/// conséquence pratique est celle qui compte pour cet outil : rendre obligatoire ce qui était facultatif
/// est une rupture, et c'est précisément ce que le comparateur de 003 sait déjà détecter.
/// </remarks>
public sealed class OptionalParticleRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public OptionalParticleRule()
        : base("`minOccurs=\"0\"`", "absent from `required`", ProjectionConfidence.Carried)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) => subject?.Particle is not null;

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        if (subject.Particle!.IsRequired)
        {
            into.Append("required", subject.Particle.Name);
        }
    }
}
