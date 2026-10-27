using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
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
        verification.Marked.Should().BeTrue(string.Join(" · ", verification.Complaints));

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
    public void Every_derived_element_is_marked_and_nothing_copied_is()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        string declaration = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        foreach (CorrectionMark mark in built.Value.Marks)
        {
            mark.IsDerived.Should().BeTrue(mark.Target.Display + " vient du contrat");

            if (PointerNavigator.Find(corrected.Root, mark.Target.Json) is MappingNode)
            {
                PointerNavigator.Find(
                    corrected.Root, mark.Target.Child(CorrectionMark.Member).Child("wsdl").Json)
                    .Should().NotBeNull(
                        mark.Target.Display + " est un objet : il porte sa trace lui-même");

                continue;
            }

            // Un champ scalaire ne peut pas porter un membre d'extension : OpenAPI ne les autorise que
            // sur des objets. Sa trace figure dans la déclaration du document, qui le nomme.
            declaration.Should().Contain(
                "champ: " + mark.Target.Json,
                mark.Target.Display + " est un champ : la déclaration doit le nommer");
        }

        // Les routes que la production portait et que personne n'a retenues : recopiées à l'octet, et
        // donc sans aucune trace. Une trace y figurant signifierait que l'outil a touché ce qu'il
        // promettait de ne pas toucher.
        foreach (string untouched in new[] { "/paths/~1dossiers/get", "/paths/~1documents~1{id}/get" })
        {
            PointerNavigator.Find(corrected.Root, untouched).Should().NotBeNull();

            PointerNavigator.Find(corrected.Root, untouched + "/" + CorrectionMark.Member)
                .Should().BeNull(untouched + " a été recopiée, et ne porte donc aucune trace");
        }
    }

    [Fact]
    public void Every_place_the_projection_could_not_carry_holds_a_marker_and_the_count_is_declared()
    {
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        built.Value.Plan.Unfilled.Should().NotBeEmpty();

        SpecDocument corrected = run.Reread(built.Value.Serialized);

        foreach (IGrouping<SpecPointer, UnfilledMarker> at in built.Value.Plan.Unfilled.GroupBy(
            marker => marker.Target))
        {
            PointerNavigator.Find(
                corrected.Root, at.Key.Child(UnfilledMarker.Member).Json)
                .Should().BeOfType<SequenceNode>()
                .Which.Items.Should().HaveCount(at.Count(), at.Key.Display + " doit tout dire");
        }

        PointerNavigator.Find(
            corrected.Root,
            "/" + CorrectionBuilder.Declaration + "/" + UnfilledMarker.Count)
            .Should().BeOfType<ScalarNode>()
            .Which.Text.Should().Be(
                built.Value.Plan.Unfilled.Count.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
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
}
