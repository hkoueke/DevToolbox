using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Optimization;

/// <summary>
/// Ce que le chiffrage annonce, et ce qu'il refuse de recommander.
/// </summary>
public sealed class SavingsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-chiffrage-" + Guid.NewGuid().ToString("N"));

    public SavingsTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_measured_group_carries_every_field_a_decision_needs()
    {
        OptimizationPlan plan = await PlanAsync();

        RedundancyGroup group = plan.Groups
            .Should().Contain(group => group.RuleId == "AUD-100").Which;

        group.Count.Should().Be(14);
        group.Kind.Should().Be(CandidateKind.Parameter);
        group.Tier.Should().Be(DuplicateTier.Exact);
        group.Fingerprint.Should().NotBeEmpty();
        group.Profile.Should().Be("strict");
        group.Occurrences.Should().HaveCount(14).And.OnlyContain(occurrence => occurrence.Bytes > 0);
        group.SuggestedComponentName.Should().Be("XServiceEmetteur");

        group.Savings.CurrentBytes.Should().BeGreaterThan(group.Savings.ProjectedBytes);
        group.Savings.AbsoluteSaving.Should().Be(
            group.Savings.CurrentBytes - group.Savings.ProjectedBytes);
        group.Savings.PercentSaving.Should().BeGreaterThan(0);
        group.Savings.BreakEvenOccurrences.Should().Be(2);
        group.Savings.Safety.Should().Be(SafetyClass.Safe);
        group.Savings.Recommended.Should().BeTrue();
    }

    [Fact]
    public void A_group_below_its_break_even_count_reports_a_loss_and_no_recommendation()
    {
        // Une occurrence de cent octets que remplacerait une référence de quatre-vingt-dix : il faut
        // onze occurrences pour que l'extraction commence à gagner, et ce groupe n'en a que deux.
        SavingsEstimate estimate = SavingsEstimate.For(100, 2, 90, SafetyClass.Safe);

        estimate.BreakEvenOccurrences.Should().Be(11);
        estimate.AbsoluteSaving.Should().BeLessThanOrEqualTo(0);
        estimate.Recommended.Should().BeFalse(
            "une extraction qui ne gagne rien n'est pas une amélioration");
    }

    [Fact]
    public void An_occurrence_no_longer_than_its_reference_has_no_break_even_count_at_all()
    {
        SavingsEstimate estimate = SavingsEstimate.For(40, 50, 40, SafetyClass.Safe);

        estimate.BreakEvenOccurrences.Should().BeNull(
            "l'extraction ne gagne à aucun compte : annoncer un seuil reviendrait à recommander "
            + "d'allonger le fichier");

        estimate.Recommended.Should().BeFalse();
    }

    [Fact]
    public void A_saving_the_tool_cannot_prove_safe_is_never_a_recommendation()
    {
        SavingsEstimate estimate = SavingsEstimate.For(200, 14, 40, SafetyClass.Unsafe);

        estimate.AbsoluteSaving.Should().BeGreaterThan(0);
        estimate.Recommended.Should().BeFalse();
    }

    [Fact]
    public void The_percentage_agrees_with_the_bytes()
    {
        SavingsEstimate estimate = SavingsEstimate.For(200, 14, 40, SafetyClass.Safe);

        estimate.CurrentBytes.Should().Be(200 * 14);
        estimate.ProjectedBytes.Should().Be(200 + (40 * 14));
        estimate.AbsoluteSaving.Should().Be((200 * 14) - (200 + (40 * 14)));
        estimate.PercentSaving.Should().BeApproximately(
            estimate.AbsoluteSaving * 100d / estimate.CurrentBytes, 0.05);
    }

    [Fact]
    public async Task A_group_the_tool_cannot_act_on_is_counted_rather_than_dropped()
    {
        // Deux schémas nommés identiques au mot près : la répétition est réelle et mesurée, et pourtant
        // aucune transformation ne la couvre — un composant ne s'extrait pas dans les composants, et
        // choisir lequel des deux noms survit n'est pas une décision d'outil.
        OptimizationPlan plan = await PlanAsync();

        plan.Groups.Should().Contain(group => group.RuleId == "AUD-104");

        plan.Uncovered.Select(group => group.RuleId)
            .Should().Contain("AUD-104");

        plan.Transformations.Should().NotContain(
            transformation => transformation.FromRuleId == "AUD-104");
    }

    [Fact]
    public async Task A_media_type_repeated_in_a_response_is_never_proposed_as_a_request_body()
    {
        // La famille de destination se lit sur l'emplacement de la chose répétée. La lire sur la règle
        // ferait ranger un type de média de réponse dans « components.requestBodies », et produirait une
        // description qui ne se relit pas.
        OptimizationPlan plan = await PlanAsync();

        plan.Transformations.Should().NotContain(transformation =>
            transformation.Origins.Any(origin =>
                origin.Json.Contains("/responses/", StringComparison.Ordinal))
            && transformation.RuleId == "OPT-004");
    }

    [Fact]
    public async Task Every_proposal_stays_a_proposal_and_names_how_to_undo_itself()
    {
        OptimizationPlan plan = await PlanAsync();

        plan.Transformations.Should().NotBeEmpty();

        plan.Transformations.Should().OnlyContain(
            transformation => transformation.State == TransformationState.Proposed);

        plan.AppliedCount.Should().Be(0, "le plan est un devis");

        plan.Transformations.Should().OnlyContain(
            transformation => transformation.Reversal.Text.Length > 0);
    }

    [Fact]
    public async Task The_growth_check_refuses_a_projection_that_would_exceed_what_is_tolerated()
    {
        // Aucune croissance tolérée par défaut, et le plan ne fait que réduire : rien n'est dépassé.
        OptimizationPlan plan = await PlanAsync();

        plan.Growth.Exceeded.Should().BeFalse();
        plan.Publishable.Should().BeTrue();

        // Une projection plus grosse que l'entrée dépasse, et c'est au plan de le dire.
        GrowthCheck.For(1000, -500, 0).Exceeded.Should().BeTrue();
        GrowthCheck.For(1000, -50, 10).Exceeded.Should().BeFalse(
            "une croissance de 5 % reste sous les 10 % tolérés");
    }

    [Fact]
    public async Task A_disabled_optimization_rule_produces_no_proposal()
    {
        Ruleset ruleset = Ruleset.Default with
        {
            Rules = new Dictionary<string, RuleSetting>(StringComparer.Ordinal)
            {
                ["OPT-002"] = RuleSetting.Default with { Enabled = false },
            },
        };

        OptimizationPlan plan = await PlanAsync(ruleset);

        plan.Transformations.Should().NotContain(
            transformation => transformation.RuleId == "OPT-002");

        // Le groupe reste mesuré : c'est la proposition qui est désactivée, pas le constat.
        plan.Groups.Should().Contain(group => group.RuleId == "AUD-100");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Chiffre le fichier de corpus que l'examen prend pour sujet.</summary>
    private async Task<OptimizationPlan> PlanAsync(Ruleset? ruleset = null)
    {
        SpecDocument document = await AuditHarness.ReadAsync(_root, "redundant.yaml");
        AuditResult audit = AuditHarness.Audit(document, ruleset);

        return TransformationPlanner.Plan(document, audit, ruleset ?? Ruleset.Default);
    }
}
