using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Les types gregoriens et « duration » se projettent en chaîne, à peu près.</summary>
/// <remarks>
/// La forme survit, le sens ne survit pas. Un « xs:gYearMonth » est une chaîne dont « 2026-10 » est la
/// seule écriture licite ; projeté en « type: string » sans format, il accepte « bonjour ». Aucun format
/// JSON Schema ne couvre ces types, et en inventer un — « x-gYearMonth » — ferait croire qu'un outil
/// tiers le vérifierait.
/// </remarks>
public sealed class GregorianTypeRule : ProjectionRule
{
    private static readonly string[] Gregorian =
    [
        "duration", "gYear", "gYearMonth", "gMonth", "gMonthDay", "gDay",
        "time", "hexBinary", "QName", "NOTATION", "anyURI",
    ];

    /// <summary>Crée la règle.</summary>
    public GregorianTypeRule()
        : base(
            "`xsd:duration`, `gYear`, `gMonthDay`, and the other gregorian types",
            "`type: string`",
            ProjectionConfidence.Approximate,
            ProjectionGapReason.NoTypedCounterpart)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { } type && Gregorian.Contains(Local(type.BaseType), StringComparer.Ordinal);

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        into.Set("type", "string");

        Record(subject, "type « " + Local(subject.Type!.BaseType) + " »", gaps);
    }

    private static string Local(string? qualified)
    {
        if (qualified is null)
        {
            return string.Empty;
        }

        int separator = qualified.LastIndexOf(':');

        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }
}
