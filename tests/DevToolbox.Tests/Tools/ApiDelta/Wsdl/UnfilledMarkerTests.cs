using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// Là où la projection n'a rien porté, le document le dit-il — ou bien l'invente-t-il ?
/// </summary>
/// <remarks>
/// Un schéma plausible est la seule sortie dont un lecteur ne peut pas récupérer le fait que personne ne
/// sait. Un attribut XSD n'a aucune contrepartie en JSON Schema : le produire comme une propriété
/// ordinaire serait affirmer que la production doit porter un champ que le contrat n'a jamais décrit, et
/// rien dans le document ne permettrait de s'en apercevoir.
/// </remarks>
public sealed class UnfilledMarkerTests
{
    private const string Response = "DocumentDetaille";

    [Fact]
    public void A_response_type_using_an_attribute_yields_a_marker_and_never_a_plausible_schema()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("lireDocument")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Plan.Unfilled.Should().NotBeEmpty(
            "le type de réponse de « lireDocument » porte deux attributs XSD");

        built.Value.Plan.Unfilled.Should().Contain(
            marker => marker.Construct.Contains("version", StringComparison.Ordinal),
            "l'attribut « version » doit être nommé, et non deviné");

        SpecDocument corrected = run.Reread(built.Value.Serialized);
        string schema = "/components/schemas/" + Response;

        PointerNavigator.Find(corrected.Root, schema).Should().NotBeNull(
            "le type de réponse est porté, parce que la route retenue le référence");

        foreach (string attribute in new[] { "version", "verrouille" })
        {
            PointerNavigator.Find(corrected.Root, schema + "/properties/" + attribute)
                .Should().BeNull(
                    "« " + attribute + " » est un attribut XSD, et non une propriété : l'inventer "
                        + "ferait croire que le contrat décrit un champ dont il ne parle pas");
        }

        PointerNavigator.Find(corrected.Root, schema + "/" + UnfilledMarker.Member)
            .Should().BeOfType<SequenceNode>(
                "le schéma doit dire ce que la projection n'a pas porté de lui");
    }

    [Fact]
    public void An_operation_says_on_itself_what_its_payload_could_not_carry()
    {
        // Un marqueur posé seulement sur le composant laisserait croire, à qui lit l'opération, que sa
        // réponse est complète. C'est l'opération que le développeur lit pour décider.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("lireDocument")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = run.Reread(built.Value.Serialized);
        SpecNode? posted = PointerNavigator.Find(
            corrected.Root, "/paths/~1documents/get/" + UnfilledMarker.Member);

        SequenceNode markers = posted.Should().BeOfType<SequenceNode>().Subject;

        markers.Items.Should().NotBeEmpty();

        foreach (SpecNode item in markers.Items)
        {
            IEnumerable<string> keys = item.Should().BeOfType<MappingNode>().Subject
                .Entries.Select(entry => entry.Key.Text);

            keys.Should().Contain(["construction", "dans", "raison"], "un marqueur dit quoi, où et pourquoi");
        }
    }

    [Fact]
    public void The_count_the_document_declares_is_the_count_it_carries_and_the_count_the_report_states()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        CorrectionSelection selection = new([run.Route("lireDocument"), run.Route("dossierModifie")]);
        Result<BuiltCorrection> built = run.Build(selection);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        int announced = built.Value.Plan.Unfilled.Count;

        announced.Should().BeGreaterThan(0);

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        Declared(corrected).Should().Be(
            announced, "un document inachevé doit être reconnaissable comme inachevé depuis lui seul");

        Posted(corrected.Root).Should().Be(
            announced, "le nombre déclaré doit se recouper avec les marqueurs posés");

        string report = Encoding.UTF8.GetString(
            MarkdownReportWriter.Write(Run(run, built.Value)).Span);

        report.Should().Contain(
            "Endroits non remplis : " + announced.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "le rapport et le document doivent dire le même nombre");
    }

    [Fact]
    public void The_verification_expects_the_markers_instead_of_reading_them_as_a_missing_change()
    {
        // L'endroit qu'un marqueur occupe est précisément celui où la projection n'avait rien à porter :
        // exiger qu'il ait changé reviendrait à exiger le schéma que l'outil a refusé de deviner.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("lireDocument")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        CorrectionVerification verification = run.Verify(built.Value);

        verification.Marked.Should().BeTrue(string.Join(" · ", verification.Complaints));
        verification.Established.Should().BeTrue(verification.Explanation);
    }

    private static int Declared(SpecDocument corrected)
    {
        SpecNode? node = PointerNavigator.Find(
            corrected.Root,
            "/" + CorrectionBuilder.Declaration + "/" + UnfilledMarker.Count);

        return int.Parse(
            node.Should().BeOfType<ScalarNode>().Subject.Text,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Les marqueurs réellement posés dans le document, où qu'ils soient.</summary>
    private static int Posted(SpecNode node)
    {
        int posted = 0;

        switch (node)
        {
            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    posted += string.Equals(
                        entry.Key.Text, UnfilledMarker.Member, StringComparison.Ordinal)
                        && entry.Value is SequenceNode sequence
                            ? sequence.Items.Count
                            : Posted(entry.Value);
                }

                break;

            case SequenceNode items:
                foreach (SpecNode item in items.Items)
                {
                    posted += Posted(item);
                }

                break;

            default:
                break;
        }

        return posted;
    }

    /// <summary>L'exécution telle que les rapports la lisent, réduite à ce que cet essai regarde.</summary>
    private static ApiDeltaRun Run(WsdlCorrectionRun run, BuiltCorrection built) => new(
        Guid.Empty,
        new RunSelection(RunMode.Consolidate, "production.yaml", "sans objet", "artefacts"),
        DateTimeOffset.UnixEpoch,
        TimeSpan.Zero,
        run.Production,
        run.Candidate,
        [],
        run.Differences,
        [],
        Correction: new CorrectionOutcome(built.Plan, CorrectionVerification.NotRun, Written: true),
        Footing: run.Footing);
}
