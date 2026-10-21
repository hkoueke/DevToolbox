using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Comparison;

/// <summary>
/// Ce que l'outil dit du changement, et avec quelle assurance.
/// </summary>
public sealed class ClassificationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-classement-" + Guid.NewGuid().ToString("N"));

    public ClassificationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_removed_response_breaks_the_callers_that_handled_it()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-breaking.yaml");

        Difference removed = Single(
            differences, "/paths/~1documents~1{id}/get/responses/404", DifferenceKind.Removed);

        removed.Impact.Should().Be(ChangeImpact.Breaking);
        removed.Before.Should().NotBeNull();
        removed.After.Should().BeNull();
        removed.Rationale.Should().NotBeEmpty();
        removed.RecommendedAction.Should().Contain("majeure");
    }

    [Fact]
    public async Task A_parameter_that_becomes_required_breaks_the_callers_that_omitted_it()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-breaking.yaml");

        Difference required = differences.Single(difference =>
            string.Equals(
                difference.ChangeId, ChangeIds.ParameterBecameRequired, StringComparison.Ordinal));

        required.Impact.Should().Be(ChangeImpact.Breaking);
        required.Confidence.Should().Be(Confidence.Certain);
    }

    [Fact]
    public async Task A_narrowed_constraint_breaks_and_says_which_way_it_moved()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-breaking.yaml");

        IReadOnlyList<Difference> narrowed =
        [
            .. differences.Where(difference =>
                string.Equals(difference.ChangeId, ChangeIds.ConstraintNarrowed, StringComparison.Ordinal)),
        ];

        narrowed.Should().HaveCount(2, "deux maxLength passent de 256 à 128");
        narrowed.Should().OnlyContain(difference => difference.Impact == ChangeImpact.Breaking);
        narrowed.Should().OnlyContain(difference => difference.Confidence == Confidence.Certain);
    }

    [Fact]
    public async Task A_removed_enum_value_breaks()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-breaking.yaml");

        IReadOnlyList<Difference> removed =
        [
            .. differences.Where(difference =>
                difference.Kind == DifferenceKind.Removed
                && difference.Location.Json.Contains("enum", StringComparison.Ordinal)),
        ];

        removed.Should().HaveCount(2, "la valeur « archive » disparaît de deux énumérations");
        removed.Should().OnlyContain(difference => difference.Impact == ChangeImpact.Breaking);
    }

    [Fact]
    public async Task Nothing_in_a_breaking_candidate_is_called_compatible_by_mistake()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-breaking.yaml");

        // Chaque différence de ce candidat est un resserrement. Aucune ne doit passer pour compatible :
        // c'est l'erreur qui coûte le plus cher, puisqu'elle se découvre chez l'appelant.
        differences.Should().OnlyContain(difference => difference.Impact == ChangeImpact.Breaking);
    }

    [Fact]
    public async Task An_added_operation_a_new_parameter_and_a_new_schema_are_compatible()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-additive.yaml");

        differences.Should().NotBeEmpty();
        differences.Should().OnlyContain(difference => difference.Kind == DifferenceKind.Added);

        // Le nouveau chemin, le nouveau paramètre facultatif et le nouveau schéma sont là.
        differences.Should().Contain(difference =>
            difference.Location.Json.Contains("~1dossiers~1{id}", StringComparison.Ordinal));
        differences.Should().Contain(difference =>
            difference.Location.Json.Contains("DossierDetail", StringComparison.Ordinal));

        differences.Should().NotContain(difference => difference.Impact == ChangeImpact.Breaking);
    }

    [Fact]
    public async Task A_component_that_keeps_its_name_while_changing_its_contract_is_not_called_compatible()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-conflict.yaml");

        differences.Should().NotBeEmpty();
        differences.Should().Contain(difference =>
            difference.Location.Json.Contains("Dossier", StringComparison.Ordinal));
        differences.Should().NotContain(difference =>
            difference.Impact == ChangeImpact.DocumentationOnly);
    }

    [Fact]
    public async Task Every_difference_carries_a_location_a_rationale_and_an_action()
    {
        foreach (string candidate in new[]
        {
            "candidate-breaking.yaml", "candidate-additive.yaml", "candidate-doc-only.yaml",
            "candidate-conflict.yaml",
        })
        {
            IReadOnlyList<Difference> differences = await CompareAsync(candidate);

            differences.Should().OnlyContain(difference =>
                difference.Location.Json.Length > 0
                && difference.Location.Display.Length > 0
                && difference.Rationale.Length > 0
                && difference.RecommendedAction.Length > 0
                && difference.ChangeId.Length > 0);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Difference Single(
        IReadOnlyList<Difference> differences,
        string pointer,
        DifferenceKind kind) =>
        differences.Single(difference =>
            string.Equals(difference.Location.Json, pointer, StringComparison.Ordinal)
            && difference.Kind == kind);

    private async Task<IReadOnlyList<Difference>> CompareAsync(string candidateName)
    {
        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        SpecReadResult candidate = await reader.ReadAsync(
            Write(candidateName), DocumentRole.Candidate, LoadLimits.Default, CancellationToken.None);

        SpecReadResult production = await reader.ReadAsync(
            Write("baseline-30.yaml"), DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        candidate.IsFaithful.Should().BeTrue();
        production.IsFaithful.Should().BeTrue();

        return new DocumentComparer(new OpenApiMetamodel())
            .Compare(candidate.Document!, production.Document!, CancellationToken.None)
            .Value;
    }

    private string Write(string fileName)
    {
        string path = Path.Combine(_root, fileName);
        File.WriteAllText(path, CorpusLoader.Read(fileName));

        return path;
    }
}
