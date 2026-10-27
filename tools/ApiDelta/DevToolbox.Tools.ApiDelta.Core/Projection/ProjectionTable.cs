using DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>La table de projection publiée, rendue exécutable.</summary>
/// <remarks>
/// L'ordre compte, et il est celui de la table. Une règle portée s'applique avant une règle approchante,
/// qui s'applique avant une règle qui ne porte rien : un type qui relève de plusieurs lignes — une
/// extension dont la base est un type simple, par exemple — doit porter ce qui se porte avant de
/// consigner ce qui ne se porte pas.
/// <para>
/// Un essai lit le fichier publié et vérifie que ce jeu le couvre exactement. C'est ce qui empêche la
/// table et le code de divager : l'une ou l'autre peut changer, mais pas seule.
/// </para>
/// </remarks>
public static class ProjectionTable
{
    /// <summary>Les règles, dans l'ordre de la table publiée.</summary>
    public static IReadOnlyList<IProjectionRule> Rules { get; } =
    [
        new SequenceElementRule(),
        new OptionalParticleRule(),
        new RepeatedParticleRule(),
        new StringFacetRule(),
        new EnumerationRule(),
        new RangeFacetRule(),
        new NumericPrecisionRule(),
        new SimpleTypeFormatRule(),
        new GregorianTypeRule(),
        new NillableRule(),
        new ChoiceRule(),
        new ExtensionRule(),
        new RestrictionRule(),
        new AnyContentRule(),
        new AllGroupRule(),
        new SoapFaultRule(),
        new SoapHeaderRule(),
        new AttributeRule(),
        new SubstitutionGroupRule(),
        new MixedContentRule(),
        new NamespaceRule(),
        new BindingRule(),
        new ParticleOrderRule(),
        new Xsd11Rule(),
    ];

    /// <summary>Les règles qui ont quelque chose à dire sur ce sujet.</summary>
    /// <param name="subject">Le sujet.</param>
    /// <returns>Les règles applicables, dans l'ordre de la table.</returns>
    public static IEnumerable<IProjectionRule> For(ProjectionSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return Rules.Where(rule => rule.Matches(subject));
    }
}
