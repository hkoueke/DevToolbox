using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un « maxOccurs » supérieur à un devient un tableau borné.</summary>
/// <remarks>
/// « unbounded » laisse « maxItems » absent, plutôt que de poser une borne arbitraire : une borne inventée
/// serait lue comme venant du contrat, et un client s'y conformerait.
/// </remarks>
public sealed class RepeatedParticleRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public RepeatedParticleRule()
        : base(
            "`maxOccurs` greater than one",
            "`type: array` with bounds",
            ProjectionConfidence.Carried)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Particle is { IsCollection: true };

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        SchemaDraft property = into.Property(subject.Particle!.Name);

        property.Set("type", "array");

        if (subject.Particle.MinOccurs > 0)
        {
            property.Set("minItems", (long)subject.Particle.MinOccurs);
        }

        if (subject.Particle.MaxOccurs is { } bound)
        {
            property.Set("maxItems", (long)bound);
        }
    }
}
