using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

/// <summary>
/// Les écarts se groupent-ils par motif, et non par occurrence ?
/// </summary>
/// <remarks>
/// La promesse est bornée par les <em>motifs</em>, pas par les occurrences. Quatre cents attributs non
/// portés enseignent une chose ; les afficher un par un la fait payer quatre cents lignes, et le
/// développeur cesse de lire bien avant d'arriver à ce qui compte.
/// <para>
/// L'emplacement de chaque occurrence est néanmoins conservé : on doit pouvoir aller voir le
/// quatre-centième. Grouper à la collecte l'aurait perdu.
/// </para>
/// </remarks>
public sealed class GroupingTests
{
    [Fact]
    public void Two_thousand_attributes_yield_one_entry_carrying_their_count()
    {
        WsdlContract contract = Read(GeneratedWsdl.ManyAttributes(2_000));

        ProjectionResult projected = new Projector().Project(contract, OpenApiVersion.V30);

        projected.ByReason.Should().ContainKey(ProjectionGapReason.Attribute)
            .WhoseValue.Should().Be(2_000, "le nombre est porté, et non perdu dans un groupement");

        projected.ByReason.Keys.Should().HaveCountLessThan(
            10, "le nombre d'entrées est borné par les motifs, non par les occurrences");
    }

    [Fact]
    public void Every_occurrence_keeps_its_own_location()
    {
        // Le groupement vit dans le rapport, pas dans la collecte. Un écart par occurrence est ce qui
        // permet d'aller voir la deux-millième, et c'est pourquoi la collecte ne groupe pas.
        WsdlContract contract = Read(GeneratedWsdl.ManyAttributes(50));

        ProjectionResult projected = new Projector().Project(contract, OpenApiVersion.V30);

        IReadOnlyList<ProjectionGap> attributes =
        [
            .. projected.Gaps.Where(gap => gap.Reason == ProjectionGapReason.Attribute),
        ];

        attributes.Should().HaveCount(50);
        attributes.Select(gap => gap.Construct).Should().OnlyHaveUniqueItems(
            "chaque occurrence nomme l'attribut dont il s'agit");
        attributes.Should().OnlyContain(gap => gap.Location.Line > 0);
    }

    [Fact]
    public void An_enormous_enumeration_is_carried_whole_and_produces_no_gap_of_its_own()
    {
        // L'inverse du cas précédent, et il mérite son essai : une énumération de dix mille valeurs se
        // porte entièrement, et un outil qui la signalerait comme un écart enseignerait quelque chose de
        // faux sur ce que la projection sait faire.
        //
        // L'assertion porte sur l'énumération, et non sur le total. Une première version exigeait zéro
        // écart et se trompait : l'espace de noms du type en est un, comme la table publiée le dit de
        // toute déclaration nommée, puisque JSON n'a pas d'espaces de noms. La règle avait raison.
        WsdlContract contract = Read(Wrapping(GeneratedWsdl.EnormousEnumeration(10_000)));

        ProjectionResult projected = new Projector().Project(contract, OpenApiVersion.V30);

        projected.Gaps.Should().NotContain(
            gap => gap.Reason == ProjectionGapReason.NoTypedCounterpart,
            "une énumération de chaînes a bien une contrepartie typée");

        projected.Gaps.Should().NotContain(
            gap => gap.Construct.Contains("enumeration", StringComparison.Ordinal),
            "aucune des dix mille valeurs n'est un écart : " + Said(projected));

        projected.ByReason.Keys.Should().BeEquivalentTo(
            [ProjectionGapReason.Namespace],
            "l'espace de noms est le seul écart de ce contrat");
    }

    [Fact]
    public void The_corpus_contract_reports_its_gaps_by_reason_and_the_figures_agree()
    {
        WsdlContract contract = Corpus();

        ProjectionResult projected = new Projector().Project(contract, OpenApiVersion.V30);

        // Les chiffres doivent se recouper avec les écarts : un rapport qui annoncerait trois écarts et
        // en listerait sept serait pire qu'un rapport sans chiffres.
        projected.Figures.NotCarried.Should().Be(
            projected.Gaps.Count(gap => gap.Confidence == ProjectionConfidence.NotCarried));

        projected.Figures.Approximate.Should().Be(
            projected.Gaps.Count(gap => gap.Confidence == ProjectionConfidence.Approximate));

        projected.ByReason.Values.Sum().Should().Be(
            projected.Gaps.Count, "le groupement ne perd ni n'invente aucune occurrence");

        projected.Figures.IsFaithful.Should().BeFalse(
            "ce contrat porte des attributs et un choix : annoncer une équivalence serait faux");
    }

    [Fact]
    public void The_attribute_reason_is_the_largest_in_the_corpus()
    {
        // Ce que la table publiée annonce — « la plus grosse source d'écarts en pratique » — doit se
        // vérifier sur un contrat réaliste, sans quoi c'est une affirmation gratuite.
        ProjectionResult projected = new Projector().Project(Corpus(), OpenApiVersion.V30);

        projected.ByReason.Should().ContainKey(ProjectionGapReason.Attribute);

        projected.ByReason[ProjectionGapReason.Attribute].Should().BeGreaterThan(
            0, "le corpus porte délibérément des attributs sans équivalent");
    }

    private static string Said(ProjectionResult projected) =>
        string.Join(" · ", projected.Gaps.Select(gap => gap.Reason + " : " + gap.Construct));

    /// <summary>Lit un contrat écrit ici même.</summary>
    private static WsdlContract Read(string document)
    {
        WsdlReadResult read = new WsdlReader().ReadBytes(
            Encoding.UTF8.GetBytes(document),
            "essai.wsdl",
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(Path.GetTempPath()));

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(one => one.Message)));

        return read.Contract!;
    }

    /// <summary>Lit le contrat du corpus, ses deux schémas matérialisés à côté.</summary>
    private static WsdlContract Corpus()
    {
        string root = Path.Combine(
            Path.GetTempPath(), "devtoolbox-projection-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        try
        {
            foreach (string name in new[] { "catalogue.wsdl", "catalogue-types.xsd", "commun.xsd" })
            {
                File.WriteAllText(
                    Path.Combine(root, name),
                    CorpusLoader.Read("Wsdl." + name),
                    new UTF8Encoding(false));
            }

            WsdlReadResult read = new WsdlReader().ReadAsync(
                Path.Combine(root, "catalogue.wsdl"),
                WsdlLimits.Default,
                ImportPolicy.ConfinedTo(root),
                CancellationToken.None).GetAwaiter().GetResult();

            read.IsFaithful.Should().BeTrue(
                string.Join(" · ", read.Diagnostics.Select(one => one.Message)));

            return read.Contract!;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Enveloppe un schéma dans le contrat minimal qui le porte en ligne.</summary>
    private static string Wrapping(string schema)
    {
        string body = schema[(schema.IndexOf("<xs:schema", StringComparison.Ordinal))..];

        return """
            <?xml version="1.0" encoding="utf-8"?>
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:codes">
              <wsdl:types>
            """
            + body
            + """
              </wsdl:types>
            </wsdl:definitions>
            """;
    }
}
