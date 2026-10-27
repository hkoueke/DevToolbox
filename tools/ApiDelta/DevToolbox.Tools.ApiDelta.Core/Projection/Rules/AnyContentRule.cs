using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Un « xsd:any » se projette en « additionalProperties », à peu près.</summary>
/// <remarks>
/// XSD distingue trois degrés de contrôle — « strict », « lax », « skip » — et JSON Schema n'en a que
/// deux : accepter ou refuser. « strict » exige que le contenu étranger soit déclaré quelque part, ce qui
/// disparaît ; « skip » ne vérifie rien, ce qui survit. Les trois se réduisent à vrai.
/// </remarks>
public sealed class AnyContentRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public AnyContentRule()
        : base(
            "`xsd:any`, `processContents`",
            "`additionalProperties`",
            ProjectionConfidence.Approximate,
            ProjectionGapReason.OpenContent)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) => subject?.Type is { Open: true };

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        into.Set("additionalProperties", true);

        Record(subject, "contenu ouvert de « " + subject.Type!.Name + " »", gaps);
    }
}
