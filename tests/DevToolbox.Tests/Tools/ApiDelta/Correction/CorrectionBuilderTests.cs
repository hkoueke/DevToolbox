using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Correction;

/// <summary>
/// Ce que la production corrigée porte, et ce qu'elle laisse exactement comme elle l'a trouvé.
/// </summary>
public sealed class CorrectionBuilderTests
{
    private readonly SpecReader _reader = new(NullLogger<SpecReader>.Instance);
    private readonly CorrectionBuilder _builder = new(
        new OperationCatalog(), new SplicingEmitter(new CanonicalEmitter()));

    [Fact]
    public void An_empty_selection_writes_nothing_and_says_why()
    {
        Result<BuiltCorrection> built = _builder.Build(Request(new CorrectionSelection()));

        built.IsFailure.Should().BeTrue("une sélection vide ne demande pas tout");
        built.Failure!.Message.Should().Contain("vide");
    }

    [Fact]
    public void A_chosen_route_arrives_and_the_document_still_parses()
    {
        OperationSelector route = new("/dossiers/{id}", HttpOperationMethod.Get);

        Result<BuiltCorrection> built = _builder.Build(Request(new CorrectionSelection([route])));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = Reread(built.Value.Serialized);

        PointerNavigator.Find(corrected.Root, "/paths/~1dossiers~1{id}/get")
            .Should().NotBeNull("la route retenue doit être là");

        built.Value.Plan.Routes.Should().Contain(route);
    }

    [Fact]
    public void Everything_the_production_said_is_still_there_word_for_word()
    {
        Result<BuiltCorrection> built = _builder.Build(
            Request(new CorrectionSelection([new("/dossiers/{id}", HttpOperationMethod.Get)])));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string before = CorpusLoader.Read("baseline-30.yaml");
        string after = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        foreach (string line in before.Split('\n').Where(line => line.Trim().Length > 0))
        {
            after.Should().Contain(
                line.TrimEnd('\r'),
                "« " + line.Trim() + " » venait de la production et personne n'a demandé de la changer");
        }
    }

    [Fact]
    public void The_written_document_carries_nothing_but_openapi()
    {
        // L'inverse de ce que cet essai vérifiait : le document se déclarait « correction proposée, non
        // publiée » dans un bloc x-apidelta-correction, et portait une trace x-apidelta-origin sur chaque
        // élément dérivé. Un fichier destiné à devenir un contrat publié ne porte que de l'OpenAPI ; ce
        // que ces blocs disaient se lit dans le rapport écrit à côté.
        Result<BuiltCorrection> built = _builder.Build(
            Request(new CorrectionSelection([new("/dossiers/{id}", HttpOperationMethod.Get)])));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string text = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        text.Should().NotContain("x-apidelta", "aucune section de l'outil ne doit rester dans le document");
        text.Should().NotContain("correction proposée, non publiée");
        text.Should().NotContain(
            "candidate-additive.yaml", "le nom du fichier source n'a rien à faire dans le contrat");
    }

    [Fact]
    public void A_carried_route_is_recorded_in_the_ledger_rather_than_in_the_document()
    {
        Result<BuiltCorrection> built = _builder.Build(
            Request(new CorrectionSelection([new("/dossiers/{id}", HttpOperationMethod.Get)])));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = Reread(built.Value.Serialized);

        PointerNavigator.Find(corrected.Root, "/paths/~1dossiers~1{id}/get")
            .Should().NotBeNull("la route retenue doit bien être portée");

        // Le registre reste tenu : c'est lui qui alimente le rapport, et le vérificateur s'en sert pour
        // savoir quels emplacements avaient le droit de changer.
        built.Value.Marks.Should().NotBeEmpty();
    }

    [Fact]
    public void A_route_the_production_already_holds_is_declined_by_naming_the_reason()
    {
        OperationSelector held = new("/dossiers", HttpOperationMethod.Get);

        Result<CorrectionPlan> plan = _builder.Plan(Request(new CorrectionSelection([held])));

        plan.IsSuccess.Should().BeTrue(plan.Failure?.Message);
        plan.Value.Routes.Should().BeEmpty();
        plan.Value.Declined.Should().ContainSingle()
            .Which.Why.Should().Contain("porte déjà cette route");
    }

    [Fact]
    public void Planning_writes_nothing()
    {
        string before = CorpusLoader.Read("baseline-30.yaml");

        Result<CorrectionPlan> plan = _builder.Plan(
            Request(new CorrectionSelection([new("/dossiers/{id}", HttpOperationMethod.Get)])));

        plan.IsSuccess.Should().BeTrue();
        CorpusLoader.Read("baseline-30.yaml").Should().Be(before, "établir un plan ne touche rien");
    }

    [Fact]
    public void One_chosen_difference_changes_that_field_and_nothing_else_of_the_route()
    {
        SpecDocument production = Read("baseline-30.yaml");
        SpecDocument candidate = Read("candidate-breaking.yaml", DocumentRole.Candidate);

        Result<IReadOnlyList<Difference>> compared =
            new DocumentComparer(new OpenApiMetamodel()).Compare(
                candidate, production, CancellationToken.None);

        compared.IsSuccess.Should().BeTrue(compared.Failure?.Message);

        IReadOnlyList<Difference> differences = compared.Value;

        Difference? scalar = differences.FirstOrDefault(difference =>
            difference.Location.Json.StartsWith("/paths/", StringComparison.Ordinal)
            && difference.Kind is DifferenceKind.Modified
            && PointerNavigator.Find(candidate.Root, difference.Location.Json) is ScalarNode);

        scalar.Should().NotBeNull("le corpus cassant doit porter au moins une modification de scalaire");

        OperationSelector route = Route(scalar.Location);

        Result<BuiltCorrection> built = _builder.Build(new CorrectionRequest(
            production,
            candidate,
            differences,
            new CorrectionSelection(
                Differences: [new DifferenceSelector(route, scalar.ChangeId, scalar.Location.Json)]),
            DependencyClosure.Empty,
            Version: null,
            CanonicalFormatting.Default));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        SpecDocument corrected = Reread(built.Value.Serialized);

        PointerNavigator.Find(corrected.Root, scalar.Location.Json).Should().NotBeNull();

        built.Value.Plan.Differences.Should().ContainSingle()
            .Which.ChangeId.Should().Be(scalar.ChangeId);
    }

    private CorrectionRequest Request(CorrectionSelection selection) => new(
        Read("baseline-30.yaml"),
        Read("candidate-additive.yaml", DocumentRole.Candidate),
        [],
        selection,
        DependencyClosure.Empty,
        Version: null,
        CanonicalFormatting.Default);

    private SpecDocument Read(string fileName, DocumentRole role = DocumentRole.Production)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(CorpusLoader.Read(fileName));
        SpecReadResult result = _reader.ReadBytes(bytes, fileName, role, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            fileName + " doit se lire : "
            + string.Join(" / ", result.Diagnostics.Select(d => d.Message)));

        return result.Document;
    }

    private SpecDocument Reread(ReadOnlyMemory<byte> bytes)
    {
        SpecReadResult result = _reader.ReadBytes(
            bytes, "corrigé", DocumentRole.Production, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            "la production corrigée doit se relire : "
            + string.Join(" / ", result.Diagnostics.Select(d => d.Message)));

        return result.Document;
    }

    private static OperationSelector Route(SpecPointer location)
    {
        IReadOnlyList<string> segments = location.Segments;

        return new OperationSelector(
            segments[1], Enum.Parse<HttpOperationMethod>(segments[2], ignoreCase: true));
    }
}
