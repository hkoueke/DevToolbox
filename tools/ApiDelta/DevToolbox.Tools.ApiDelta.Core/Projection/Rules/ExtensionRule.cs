using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Une dérivation par extension se projette en « allOf », à peu près.</summary>
/// <remarks>
/// « allOf » compose, il ne dérive pas. Le document produit exige bien la base et l'ajout, mais il ne dit
/// plus que l'un <em>est</em> l'autre : un client ne peut pas y lire qu'un « DocumentDetaille » est
/// acceptable là où un « Document » est attendu, ce que le contrat XSD affirmait.
/// </remarks>
public sealed class ExtensionRule : ProjectionRule
{
    /// <summary>Crée la règle.</summary>
    public ExtensionRule()
        : base(
            "derivation by extension",
            "`allOf`",
            ProjectionConfidence.Approximate,
            ProjectionGapReason.BaseRelationship)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { Derivation: Wsdl.XsdDerivation.Extension, BaseType: not null };

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        SchemaDraft reference = new();

        reference.Set("$ref", "#/components/schemas/" + Local(subject.Type!.BaseType!));

        into.Append("allOf", reference);

        Record(subject, "extension de « " + subject.Type.BaseType + " »", gaps);
    }

    private static string Local(string qualified)
    {
        int separator = qualified.LastIndexOf(':');

        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }
}
