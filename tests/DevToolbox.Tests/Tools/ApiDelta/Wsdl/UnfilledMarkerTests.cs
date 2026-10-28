using System.Globalization;
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
/// Là où la projection n'a rien porté, est-ce dit — ou bien inventé ?
/// </summary>
/// <remarks>
/// Un schéma plausible est la seule sortie dont un lecteur ne peut pas récupérer le fait que personne ne
/// sait. Un attribut XSD n'a aucune contrepartie en JSON Schema : le produire comme une propriété
/// ordinaire serait affirmer que la production doit porter un champ que le contrat n'a jamais décrit, et
/// rien ne permettrait de s'en apercevoir.
/// <para>
/// Ce qui n'a pas été porté se disait dans le document, par des membres <c>x-apidelta-unfilled</c>, et se
/// dit maintenant dans le rapport seul. Le document écrit est destiné à devenir un contrat publié : il ne
/// porte que de l'OpenAPI. La garantie de fond est inchangée et c'est elle que ces essais tiennent —
/// aucun champ n'est inventé, et chaque endroit non rempli est nommé quelque part.
/// </para>
/// </remarks>
public sealed class UnfilledMarkerTests
{
    private const string Response = "DocumentDetaille";

    [Fact]
    public void A_response_type_using_an_attribute_yields_a_gap_and_never_a_plausible_schema()
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
    }

    [Fact]
    public void The_report_says_what_the_payload_could_not_carry_and_where()
    {
        // Ce que le marqueur posé sur l'opération disait. Le lire dans le rapport plutôt que dans le
        // document ne change pas ce qui doit être dit : quoi, où, et pourquoi.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("lireDocument")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string report = Encoding.UTF8.GetString(
            MarkdownReportWriter.Write(Run(run, built.Value)).Span);

        foreach (UnfilledMarker marker in built.Value.Plan.Unfilled)
        {
            report.Should().Contain(
                marker.Construct, "le rapport doit nommer la construction qui n'a pas été portée");
        }

        report.Should().Contain(
            "/documents", "un endroit non rempli sans son emplacement ne se retrouve pas");
    }

    [Fact]
    public void The_written_document_carries_no_marker_of_the_tool_at_all()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        CorrectionSelection selection = new([run.Route("lireDocument"), run.Route("dossierModifie")]);
        Result<BuiltCorrection> built = run.Build(selection);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Plan.Unfilled.Should().NotBeEmpty("ces deux routes ont des endroits non remplis");

        string text = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        text.Should().NotContain(
            "x-apidelta",
            "un contrat publié ne porte pas les notes de l'outil qui l'a aidé à naître");
    }

    [Fact]
    public void The_count_the_report_states_is_the_count_the_plan_carries()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        CorrectionSelection selection = new([run.Route("lireDocument"), run.Route("dossierModifie")]);
        Result<BuiltCorrection> built = run.Build(selection);

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        int announced = built.Value.Plan.Unfilled.Count;

        announced.Should().BeGreaterThan(0);

        string report = Encoding.UTF8.GetString(
            MarkdownReportWriter.Write(Run(run, built.Value)).Span);

        report.Should().Contain(
            "Endroits non remplis : " + announced.ToString(CultureInfo.InvariantCulture),
            "le rapport est désormais le seul endroit qui porte ce nombre : il doit être juste");
    }

    [Fact]
    public void The_verification_does_not_read_an_unfilled_place_as_a_missing_change()
    {
        // L'endroit qu'un trou occupe est précisément celui où la projection n'avait rien à porter :
        // exiger qu'il ait changé reviendrait à exiger le schéma que l'outil a refusé de deviner.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(
            new CorrectionSelection([run.Route("lireDocument")]));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        CorrectionVerification verification = run.Verify(built.Value);

        verification.Established.Should().BeTrue(verification.Explanation);
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
