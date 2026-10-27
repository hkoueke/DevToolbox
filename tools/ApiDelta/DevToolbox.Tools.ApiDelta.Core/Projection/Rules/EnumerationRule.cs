using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Une « enumeration » devient un « enum ».</summary>
/// <remarks>
/// Les valeurs sont recopiées comme des chaînes, et dans l'ordre du schéma. Les convertir selon le type
/// de base — en entiers pour un « xs:int » restreint — serait plus joli et moins sûr : l'écriture
/// « 007 » est une valeur licite d'une énumération de chaînes, et la convertir la détruirait.
/// </remarks>
public sealed class EnumerationRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public EnumerationRule()
        : base("`enumeration`", "`enum`", ProjectionConfidence.Carried)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Facet is { Kind: "enumeration" };

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        into.Append("enum", subject.Facet!.Value);
    }
}
