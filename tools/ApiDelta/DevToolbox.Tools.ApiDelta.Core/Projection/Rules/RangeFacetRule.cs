using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Les quatre bornes numériques portent chacune son équivalent.</summary>
/// <remarks>
/// Attention à une différence de forme : en OpenAPI 3.0, « exclusiveMinimum » est un booléen qui qualifie
/// « minimum » ; en 3.1, c'est un nombre à part entière. La projection suit la version que la production
/// annonce, faute de quoi le document produit serait invalide pour l'une des deux.
/// </remarks>
public sealed class RangeFacetRule : ProjectionRule
{
    private static readonly string[] Carried =
        ["minInclusive", "maxInclusive", "minExclusive", "maxExclusive"];

    /// <summary>Crée la règle.</summary>
    public RangeFacetRule()
        : base(
            "`minInclusive`, `maxInclusive`, `minExclusive`, `maxExclusive`",
            "`minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`",
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

        if (!decimal.TryParse(
            facet.Value, System.Globalization.CultureInfo.InvariantCulture, out decimal bound))
        {
            return;
        }

        bool lower = facet.Kind.StartsWith("min", StringComparison.Ordinal);
        bool inclusive = facet.Kind.EndsWith("Inclusive", StringComparison.Ordinal);

        if (inclusive)
        {
            into.Set(lower ? "minimum" : "maximum", bound);

            return;
        }

        if (subject.Version == Syntax.OpenApiVersion.V31)
        {
            into.Set(lower ? "exclusiveMinimum" : "exclusiveMaximum", bound);

            return;
        }

        into.Set(lower ? "minimum" : "maximum", bound);
        into.Set(lower ? "exclusiveMinimum" : "exclusiveMaximum", true);
    }
}
