using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un « nillable="true" » devient « nullable », ou « null » dans une union de types.</summary>
/// <remarks>
/// Laquelle des deux dépend de la version que la production annonce, et ce n'est pas un détail de forme :
/// « nullable: true » n'existe plus en OpenAPI 3.1, et « type: [string, null] » n'existe pas en 3.0.
/// Choisir sans regarder produirait un document invalide pour la moitié des productions.
/// </remarks>
public sealed class NillableRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public NillableRule()
        : base(
            "`nillable=\"true\"`",
            "`nullable`, or `null` in a type union",
            ProjectionConfidence.Carried)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Particle is { Nillable: true };

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        SchemaDraft property = into.Property(subject.Particle!.Name);

        if (subject.Version == Syntax.OpenApiVersion.V31)
        {
            List<object?> union = [property.Get("type") ?? "string", "null"];

            property.Set("type", union);

            return;
        }

        property.Set("nullable", true);
    }
}
