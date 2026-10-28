using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

/// <summary>
/// La chaîne entière : d'un contrat de service et d'une production, un document engendré.
/// </summary>
/// <remarks>
/// Le scénario 11 du guide de démarrage, et le seul essai qui mette bout à bout tout ce que la tranche
/// promet : deux opérations absentes insérées comme routes neuves, une différence retenue sur une
/// opération que la production couvre déjà, et une production que rien n'a touchée.
/// <para>
/// L'intérêt d'un essai de bout en bout n'est pas de vérifier ce que les essais d'unité vérifient déjà.
/// Il est de prendre les défauts qui n'existent qu'au bout de la chaîne : la projection produisait un
/// « oneOf » qu'OpenAPI 3.0 refusait, et seul le document inséré <em>puis revalidé</em> pouvait le dire.
/// </para>
/// </remarks>
public sealed class WsdlEndToEndTests
{
    private const string Summary = "/paths/~1documents/post/summary";

    [Fact]
    public void Production_plus_exactly_what_was_chosen_and_nothing_else()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Plan.Routes.Should().HaveCount(2);
        built.Value.Plan.Differences.Should().ContainSingle();

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        PointerNavigator.Find(corrected.Root, "/paths/~1modifies/post").Should().NotBeNull(
            "« dossierModifie » devient une route neuve sous un chemin que la production n'avait pas");

        PointerNavigator.Find(corrected.Root, "/paths/~1documents/delete").Should().NotBeNull(
            "« archiverDocument » devient une méthode neuve sous un chemin que la production porte");

        PointerNavigator.Find(corrected.Root, Summary)
            .Should().BeOfType<ScalarNode>()
            .Which.Text.Should().Contain(
                "Dépose un document et rend son identifiant",
                "la différence retenue remplace ce que la production disait ici");
    }

    [Fact]
    public void The_verification_establishes_it_so_it_could_be_published()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        CorrectionVerification verification = run.Verify(built.Value);

        verification.Reparsed.Should().BeTrue();
        verification.Revalidated.Should().BeTrue(string.Join(" · ", verification.Complaints));
        verification.ReferencesResolved.Should().BeTrue(string.Join(" · ", verification.Complaints));
        verification.UnselectedUntouched.Should().BeTrue(string.Join(" · ", verification.Complaints));
        verification.SelectedAsChosen.Should().BeTrue(string.Join(" · ", verification.Complaints));

        verification.Established.Should().BeTrue(verification.Explanation);
    }

    [Fact]
    public void Everything_production_said_is_still_there_word_for_word()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string before = CorpusLoader.Read("Wsdl.production.yaml");
        string after = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        // Hors la seule ligne que la différence retenue remplace : c'est la définition de « plus
        // exactement ce qui a été choisi ».
        IEnumerable<string> kept = before.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Trim().Length > 0)
            .Where(line => !line.Contains("summary: Dépose un document", StringComparison.Ordinal));

        foreach (string line in kept)
        {
            after.Should().Contain(
                line, "« " + line.Trim() + " » venait de la production, que personne n'a touchée");
        }
    }

    [Fact]
    public void Every_derived_element_is_recorded_and_the_report_says_where_it_came_from()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        // Les traces vivaient dans le document, sous « x-apidelta-origin ». Elles vivent maintenant dans
        // le registre, que le rapport publie : l'origine d'un élément dérivé reste établie, et le contrat
        // écrit ne porte que de l'OpenAPI.
        string report = Encoding.UTF8.GetString(
            MarkdownReportWriter.Write(Reported(run, built.Value)).Span);

        built.Value.Marks.Should().NotBeEmpty();

        foreach (CorrectionMark mark in built.Value.Marks)
        {
            mark.IsDerived.Should().BeTrue(mark.Target.Display + " vient du contrat");

            PointerNavigator.Find(corrected.Root, mark.Target.Json)
                .Should().NotBeNull(mark.Target.Display + " doit avoir été porté");

            report.Should().Contain(
                mark.Target.Display, mark.Target.Display + " doit se retrouver dans le rapport");
        }

        // Les routes que la production portait et que personne n'a retenues sont recopiées à l'octet.
        foreach (string untouched in new[] { "/paths/~1dossiers/get", "/paths/~1documents~1{id}/get" })
        {
            PointerNavigator.Find(corrected.Root, untouched).Should().NotBeNull();
        }

        Encoding.UTF8.GetString(built.Value.Serialized.Span)
            .Should().NotContain("x-apidelta", "le contrat écrit ne porte aucune note de l'outil");
    }

    [Fact]
    public void Every_place_the_projection_could_not_carry_is_named_in_the_report()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Plan.Unfilled.Should().NotBeEmpty();

        // Le document n'en porte plus la trace : c'est le rapport qui doit tout dire, et chaque endroit
        // doit s'y retrouver par son emplacement — sans quoi le retrait des marqueurs aurait perdu de
        // l'information au lieu de la déplacer.
        string report = Encoding.UTF8.GetString(
            MarkdownReportWriter.Write(Reported(run, built.Value)).Span);

        foreach (IGrouping<SpecPointer, UnfilledMarker> at in built.Value.Plan.Unfilled.GroupBy(
            marker => marker.Target))
        {
            report.Should().Contain(
                at.Key.Display, at.Key.Display + " doit se retrouver dans le rapport");
        }

        string text = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        text.Should().NotContain("x-apidelta", "le contrat écrit ne porte aucune note de l'outil");
    }

    [Fact]
    public void A_projected_path_that_collides_with_production_is_reported_and_overwrites_nothing()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Plan.Collisions.Should().ContainSingle(
            "« archiverDocument » a été dérivé vers un chemin que la production porte déjà")
            .Which.Path.Should().Be("/documents");

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        // La méthode est ajoutée sous le chemin, et celle que la production portait est intacte.
        PointerNavigator.Find(corrected.Root, "/paths/~1documents/post/operationId")
            .Should().BeOfType<ScalarNode>()
            .Which.Text.Should().Be("deposerDocument");

        PointerNavigator.Find(corrected.Root, "/paths/~1documents/delete/operationId")
            .Should().BeOfType<ScalarNode>()
            .Which.Text.Should().Be("archiverDocument");
    }

    [Fact]
    public void The_production_file_is_byte_identical_afterwards_under_every_flag()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        string path = Path.Combine(
            Path.GetTempPath(), "devtoolbox-production-" + Guid.NewGuid().ToString("N") + ".yaml");

        try
        {
            File.WriteAllBytes(path, run.Production.Source.ToArray());

            byte[] before = File.ReadAllBytes(path);

            foreach (bool removals in new[] { false, true })
            {
                Result<BuiltCorrection> built = run.Build(Chosen(run), removals);

                built.IsSuccess.Should().BeTrue(built.Failure?.Message);

                built.Value.Serialized.Length.Should().BeGreaterThan(
                    before.Length, "le document engendré porte la production et ce qui a été retenu");

                File.ReadAllBytes(path).Should().Equal(
                    before,
                    "aucun drapeau n'autorise l'outil à réécrire la production : il n'existe aucun "
                        + "chemin d'ici vers l'écriture sur place");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Ce que le développeur retient : deux opérations absentes, et une différence sur une couverte.
    /// </summary>
    private static CorrectionSelection Chosen(WsdlCorrectionRun run)
    {
        OperationSelector covered = new("/documents", HttpOperationMethod.Post);

        Difference difference = run.Differences.Single(one =>
            string.Equals(one.Location.Json, Summary, StringComparison.Ordinal));

        return new CorrectionSelection(
            [run.Route("dossierModifie"), run.Route("archiverDocument")],
            [new DifferenceSelector(covered, difference.ChangeId, difference.Location.Json)]);
    }

    /// <summary>L'exécution telle que les rapports la lisent, réduite à ce que ces essais regardent.</summary>
    private static ApiDeltaRun Reported(WsdlCorrectionRun run, BuiltCorrection built) => new(
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
