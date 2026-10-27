using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Les types simples usuels portent un « type » et un « format ».</summary>
/// <remarks>
/// La correspondance des formats est une <em>convention</em>, et le rapport le dit. « format: date-time »
/// pour un « xs:dateTime » est l'usage, mais rien dans OpenAPI n'oblige un outil tiers à le vérifier :
/// annoncer une équivalence serait promettre une validation que personne ne garantit.
/// </remarks>
public sealed class SimpleTypeFormatRule : ProjectionRule
{
    private static readonly Dictionary<string, (string Type, string? Format)> Table =
        new(StringComparer.Ordinal)
        {
            ["string"] = ("string", null),
            ["normalizedString"] = ("string", null),
            ["token"] = ("string", null),
            ["boolean"] = ("boolean", null),
            ["byte"] = ("integer", "int32"),
            ["short"] = ("integer", "int32"),
            ["int"] = ("integer", "int32"),
            ["integer"] = ("integer", null),
            ["long"] = ("integer", "int64"),
            ["unsignedInt"] = ("integer", "int32"),
            ["unsignedLong"] = ("integer", "int64"),
            ["decimal"] = ("number", null),
            ["float"] = ("number", "float"),
            ["double"] = ("number", "double"),
            ["dateTime"] = ("string", "date-time"),
            ["date"] = ("string", "date"),
            ["base64Binary"] = ("string", "byte"),
        };

    /// <summary>Crée la règle.</summary>
    public SimpleTypeFormatRule()
        : base(
            "`xsd:string`, `boolean`, `int`, `long`, `decimal`, `double`, `dateTime`, `date`, `base64Binary`",
            "`type` with `format`",
            ProjectionConfidence.Carried)
    {
    }

    /// <summary>Le type et le format d'un type XSD usuel, s'il en fait partie.</summary>
    /// <param name="qualified">Son nom qualifié.</param>
    /// <returns>Le couple, ou rien.</returns>
    public static (string Type, string? Format)? Lookup(string? qualified) =>
        qualified is not null && Table.TryGetValue(Local(qualified), out (string, string?) found)
            ? found
            : null;

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { } type && Lookup(type.BaseType) is not null;

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        if (Lookup(subject.Type!.BaseType) is not { } mapped)
        {
            return;
        }

        into.Set("type", mapped.Type);

        if (mapped.Format is { } format)
        {
            into.Set("format", format);
        }
    }

    private static string Local(string qualified)
    {
        int separator = qualified.LastIndexOf(':');

        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }
}
