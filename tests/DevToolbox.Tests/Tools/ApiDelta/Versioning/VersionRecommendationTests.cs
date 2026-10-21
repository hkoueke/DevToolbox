using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Versioning;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Versioning;

/// <summary>
/// Le numéro à publier, et ce qui permet de le contester.
/// </summary>
public sealed class VersionRecommendationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-version-" + Guid.NewGuid().ToString("N"));

    public VersionRecommendationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void A_version_is_read_not_guessed()
    {
        SemanticVersion.TryParse("2.4.1", out SemanticVersion? parsed).Should().BeTrue();
        parsed!.Should().Be(new SemanticVersion(2, 4, 1));

        SemanticVersion.TryParse("3.0.0-rc.1+build7", out SemanticVersion? extended).Should().BeTrue();
        extended!.PreRelease.Should().Be("rc.1");
        extended.Build.Should().Be("build7");

        foreach (string unreadable in new[] { "2.4", "v2.4.1", "2.4.x", "", "deux" })
        {
            SemanticVersion.TryParse(unreadable, out _).Should().BeFalse(
                "« {0} » n'est pas une version sémantique", unreadable);
        }
    }

    [Fact]
    public void An_increment_resets_the_ranks_beneath_it_and_drops_the_pre_release()
    {
        SemanticVersion version = new(2, 4, 1, "rc.1");

        version.Increment(VersionImpact.Major).Should().Be(new SemanticVersion(3, 0, 0));
        version.Increment(VersionImpact.Minor).Should().Be(new SemanticVersion(2, 5, 0));
        version.Increment(VersionImpact.Patch).Should().Be(new SemanticVersion(2, 4, 2));
        version.Increment(VersionImpact.None).Should().Be(version);
    }

    [Fact]
    public async Task A_breaking_candidate_commands_a_major_and_names_what_commanded_it()
    {
        VersionRecommendation recommendation = await RecommendAsync("candidate-breaking.yaml");

        recommendation.Baseline.Should().Be(new SemanticVersion(2, 4, 1));
        recommendation.Increment.Should().Be(VersionImpact.Major);
        recommendation.Recommended.Should().Be(new SemanticVersion(3, 0, 0));

        recommendation.Contributors.Should().NotBeEmpty();
        recommendation.Contributors.Should().OnlyContain(
            contributor => contributor.ChangeId.Length > 0 && contributor.Location.Json.Length > 0);
    }

    [Fact]
    public async Task An_additive_candidate_commands_a_minor()
    {
        VersionRecommendation recommendation = await RecommendAsync("candidate-additive.yaml");

        recommendation.Increment.Should().Be(VersionImpact.Minor);
        recommendation.Recommended.Should().Be(new SemanticVersion(2, 5, 0));
    }

    [Fact]
    public async Task A_documentation_candidate_commands_a_patch()
    {
        VersionRecommendation recommendation = await RecommendAsync("candidate-doc-only.yaml");

        recommendation.Increment.Should().Be(VersionImpact.Patch);
        recommendation.Recommended.Should().Be(new SemanticVersion(2, 4, 2));
        recommendation.RequiresReview.Should().BeFalse();
    }

    [Fact]
    public async Task No_change_retains_the_current_version()
    {
        VersionRecommendation recommendation = await RecommendAsync("baseline-30.reformatted.yaml");

        recommendation.Increment.Should().Be(VersionImpact.None);
        recommendation.Recommended.Should().Be(new SemanticVersion(2, 4, 1));
        recommendation.Contributors.Should().BeEmpty();
    }

    [Fact]
    public void The_highest_impact_wins_and_only_it_is_credited()
    {
        VersionRecommendation recommendation = Recommend(
            [
                Difference(ChangeImpact.DocumentationOnly, "/info/description"),
                Difference(ChangeImpact.Breaking, "/paths/~1a/get"),
                Difference(ChangeImpact.NonBreaking, "/paths/~1b/get"),
            ]);

        recommendation.Increment.Should().Be(VersionImpact.Major);

        recommendation.Contributors.Select(contributor => contributor.Location.Json)
            .Should().Equal("/paths/~1a/get");
    }

    [Fact]
    public void An_unclassified_change_raises_the_floor_to_minor_and_asks_to_be_reviewed()
    {
        // Ni correctif — quelque chose a changé qui n'est pas de la documentation — ni majeur à lui seul.
        VersionRecommendation recommendation = Recommend(
            [
                Difference(ChangeImpact.DocumentationOnly, "/info/description"),
                Difference(ChangeImpact.Unknown, "/x-sg-catalog/hote"),
            ]);

        recommendation.Increment.Should().Be(VersionImpact.Minor);
        recommendation.RequiresReview.Should().BeTrue();
        recommendation.Unreviewed.Should().ContainSingle();
    }

    [Fact]
    public void An_unclassified_change_does_not_hide_behind_a_breaking_one()
    {
        VersionRecommendation recommendation = Recommend(
            [
                Difference(ChangeImpact.Breaking, "/paths/~1a/get"),
                Difference(ChangeImpact.Unknown, "/x-sg-catalog/hote"),
            ]);

        recommendation.Increment.Should().Be(VersionImpact.Major);
        recommendation.RequiresReview.Should().BeTrue("le doute reste à relire même sous un majeur");
    }

    [Fact]
    public async Task An_unreadable_baseline_is_reported_rather_than_guessed_at()
    {
        VersionRecommendation recommendation = await RecommendAsync(
            "candidate-doc-only.yaml", productionVersion: "deux-quatre-un");

        recommendation.Baseline.Should().BeNull();
        recommendation.Recommended.Should().BeNull();
        recommendation.BaselineProblem.Should().Contain("deux-quatre-un");
        recommendation.IsUsable.Should().BeFalse();
    }

    [Fact]
    public async Task A_configured_fallback_is_used_only_because_it_was_configured()
    {
        VersionRecommendation recommendation = await RecommendAsync(
            "candidate-doc-only.yaml",
            productionVersion: "deux-quatre-un",
            policy: VersioningPolicy.Default with { FallbackBaseline = "1.0.0" });

        recommendation.Baseline.Should().Be(new SemanticVersion(1, 0, 0));
        recommendation.Recommended.Should().Be(new SemanticVersion(1, 0, 1));
        recommendation.BaselineProblem.Should().NotBeNull("le repli se dit, il ne se devine pas");
    }

    [Fact]
    public void A_version_below_the_recommendation_is_flagged_as_such()
    {
        VersionRecommendation recommendation = Recommend(
            [Difference(ChangeImpact.Breaking, "/paths/~1a/get")],
            requested: new SemanticVersion(2, 5, 0));

        recommendation.Recommended.Should().Be(new SemanticVersion(3, 0, 0));
        recommendation.Override!.BelowRecommendation.Should().BeTrue();
        recommendation.Override.Forced.Should().BeFalse();
        recommendation.Effective.Should().Be(new SemanticVersion(2, 5, 0));
    }

    [Fact]
    public void A_version_above_the_recommendation_is_nobody_s_business()
    {
        VersionRecommendation recommendation = Recommend(
            [Difference(ChangeImpact.DocumentationOnly, "/info/description")],
            requested: new SemanticVersion(9, 0, 0));

        recommendation.Override!.BelowRecommendation.Should().BeFalse();
    }

    [Fact]
    public void The_policy_in_force_is_recorded_whatever_it_was()
    {
        VersionRecommendation recommendation = Recommend([]);

        recommendation.ResolvedPolicy.Should().ContainKey(VersioningPolicyKeys.EnumValueAdded);
        recommendation.ResolvedPolicy[VersioningPolicyKeys.EnumValueAdded]
            .Should().Be(VersionImpact.Major, "la valeur par défaut, et le rapport doit le dire");
    }

    [Fact]
    public void A_configurable_case_follows_the_configuration()
    {
        Difference enumAdded = new(
            ChangeIds.EnumValueAdded,
            SpecPointer.Root.Child("components").Child("schemas").Child("Document")
                .Child("properties").Child("etat").Child("enum").Item(3),
            DifferenceKind.Added,
            null,
            null,
            ChangeImpact.Breaking,
            Confidence.Probable,
            "raison",
            "action",
            VersioningPolicyKeys.EnumValueAdded);

        Recommend([enumAdded]).Increment.Should().Be(VersionImpact.Major, "la politique par défaut");

        Recommend([enumAdded], policy: VersioningPolicy.Default with
        {
            EnumValueAdded = VersionImpact.Minor,
        }).Increment.Should().Be(VersionImpact.Minor, "l'équipe en a décidé autrement");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Difference Difference(ChangeImpact impact, string pointer)
    {
        SpecPointer location = SpecPointer.Root;

        foreach (string segment in pointer.TrimStart('/').Split('/'))
        {
            location = location.Child(segment.Replace("~1", "/", StringComparison.Ordinal));
        }

        return new Difference(
            ChangeIds.FieldChanged,
            location,
            DifferenceKind.Modified,
            null,
            null,
            impact,
            Confidence.Probable,
            "raison",
            "action");
    }

    /// <summary>
    /// Recommande à partir de différences fabriquées et d'une production minimale : ce qui est éprouvé est
    /// l'arbitrage, et non la comparaison qui l'a alimenté.
    /// </summary>
    private VersionRecommendation Recommend(
        IReadOnlyList<Difference> differences,
        SemanticVersion? requested = null,
        VersioningPolicy? policy = null)
    {
        SpecDocument production = ReadAsync("baseline-30.yaml").GetAwaiter().GetResult();

        return new VersionRecommender().Recommend(
            differences, production, policy ?? VersioningPolicy.Default, requested);
    }

    private async Task<VersionRecommendation> RecommendAsync(
        string candidateName,
        string? productionVersion = null,
        VersioningPolicy? policy = null)
    {
        SpecDocument production = await ReadAsync("baseline-30.yaml", productionVersion);
        SpecDocument candidate = await ReadAsync(candidateName);

        IReadOnlyList<Difference> differences = new DocumentComparer(new OpenApiMetamodel())
            .Compare(candidate, production, CancellationToken.None)
            .Value;

        return new VersionRecommender().Recommend(
            differences, production, policy ?? VersioningPolicy.Default);
    }

    private async Task<SpecDocument> ReadAsync(string fileName, string? version = null)
    {
        string content = CorpusLoader.Read(fileName);

        if (version is not null)
        {
            content = content.Replace("  version: 2.4.1", "  version: " + version, StringComparison.Ordinal);
        }

        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + "-" + fileName);
        await File.WriteAllTextAsync(path, content);

        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }
}
