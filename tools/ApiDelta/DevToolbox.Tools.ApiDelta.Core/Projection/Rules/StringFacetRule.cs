using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>« maxLength », « minLength » et « pattern » portent le même nom des deux côtés.</summary>
/// <remarks>
/// Le même nom, et la même chose — à une réserve près qui n'en est pas une ici : XSD emploie les
/// expressions régulières de XML Schema et JSON Schema celles d'ECMA 262. Les deux dialectes diffèrent
/// sur des constructions que les contrats de service n'emploient pratiquement jamais, et le motif est
/// recopié tel quel plutôt que traduit, parce qu'une traduction silencieuse serait pire qu'une copie.
/// </remarks>
public sealed class StringFacetRule : ProjectionRule
{
    private static readonly string[] Carried = ["maxLength", "minLength", "pattern", "length"];

    /// <summary>Crée la règle.</summary>
    public StringFacetRule()
        : base(
            "`maxLength`, `minLength`, `pattern`",
            "the same keywords",
            ProjectionConfidence.Carried)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Facet is { } facet && Carried.Contains(facet.Kind, StringComparer.Ordinal);

    /// <inheritdoc />
    public override void Apply(
        ProjectionSubject subject, SchemaDraft into, ICollection<ProjectionGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(into);

        Wsdl.XsdFacet facet = subject.Facet!;

        // « length » n'a pas d'équivalent unique : il vaut les deux bornes à la fois.
        if (string.Equals(facet.Kind, "length", StringComparison.Ordinal))
        {
            Number(into, "minLength", facet.Value);
            Number(into, "maxLength", facet.Value);

            return;
        }

        if (string.Equals(facet.Kind, "pattern", StringComparison.Ordinal))
        {
            into.Set("pattern", facet.Value);

            return;
        }

        Number(into, facet.Kind, facet.Value);
    }

    private static void Number(SchemaDraft into, string keyword, string value)
    {
        if (long.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out long parsed))
        {
            into.Set(keyword, parsed);
        }
    }
}
