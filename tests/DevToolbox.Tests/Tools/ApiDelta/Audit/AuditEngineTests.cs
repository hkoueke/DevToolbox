using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Audit;

/// <summary>
/// Ce que le moteur d'examen doit garantir, indépendamment de ce que les règles trouvent.
/// </summary>
public sealed class AuditEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-examen-" + Guid.NewGuid().ToString("N"));

    public AuditEngineTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_repetition_is_one_finding_carrying_all_its_locations()
    {
        // Quatorze opérations portent le même en-tête écrit sur place. C'est une décision à prendre, pas
        // quatorze : un constat par occurrence rendrait le rapport illisible et la décision invisible.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        Finding repeated = AuditHarness.Of(audit, "AUD-100").Should().ContainSingle().Subject;

        repeated.Locations.Should().HaveCount(14);
        repeated.Evidence!.Occurrences.Should().Be(14);
        repeated.Message.Should().Contain("14");
    }

    [Fact]
    public async Task Every_registered_rule_reports_its_own_outcome()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<string> registered =
        [
            .. AuditHarness.Wired().Select(rule => rule.RuleId).OrderBy(id => id, StringComparer.Ordinal),
        ];

        audit.Outcomes.Select(outcome => outcome.RuleId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Should().Equal(registered, "une règle qui ne dit rien d'elle-même ne dit pas qu'elle a tourné");
    }

    [Fact]
    public void A_rule_written_but_never_wired_is_caught()
    {
        // Le catalogue est publié : il annonce ce que l'outil examine. Une règle au catalogue et absente
        // du câblage ferait un rapport qui se présente comme complet sans l'être.
        AuditHarness.Wired().Select(rule => rule.RuleId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Should().Equal(RuleDefaults.All.Keys.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_rule_that_could_not_run_prevents_the_audit_being_called_complete()
    {
        // Sans graphe des références, les règles qui en dépendent ne peuvent rien établir. Elles doivent
        // le dire : zéro constat et « pas évaluée » ne sont pas la même nouvelle.
        SpecDocument document = await AuditHarness.ReadAsync(_root, "redundant.yaml");

        OpenApiMetamodel metamodel = new();

        AuditResult audit = new AuditEngine(
                AuditHarness.Wired(), metamodel, new Fingerprinter(new Canonicalizer(metamodel)))
            .Run(new AuditRequest(document, Ruleset.Default), CancellationToken.None);

        audit.IsComplete.Should().BeFalse();
        audit.NotEvaluated.Should().NotBeEmpty();

        AuditHarness.Outcome(audit, "AUD-002").State.Should().Be(RuleState.NotEvaluated);
        AuditHarness.Outcome(audit, "AUD-002").Reason.Should().NotBeNullOrWhiteSpace();
        AuditHarness.Of(audit, "AUD-002").Should().BeEmpty();
    }

    [Fact]
    public async Task An_audit_whose_rules_all_ran_is_complete()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        audit.NotEvaluated.Should().BeEmpty(
            string.Join(
                " · ",
                audit.NotEvaluated.Select(outcome => outcome.RuleId + " : " + outcome.Reason)));

        audit.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task A_disabled_rule_is_reported_as_disabled_rather_than_silent()
    {
        Ruleset ruleset = Ruleset.Default with
        {
            Rules = new Dictionary<string, RuleSetting>(StringComparer.Ordinal)
            {
                ["AUD-100"] = new RuleSetting(Enabled: false),
            },
        };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        AuditHarness.Outcome(audit, "AUD-100").State.Should().Be(RuleState.Disabled);
        AuditHarness.Of(audit, "AUD-100").Should().BeEmpty();
    }

    [Fact]
    public async Task A_ruleset_can_change_a_severity_and_not_a_safety_class()
    {
        Ruleset ruleset = Ruleset.Default with
        {
            Rules = new Dictionary<string, RuleSetting>(StringComparer.Ordinal)
            {
                ["AUD-100"] = new RuleSetting(Severity: FindingSeverity.Information),
            },
        };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        Finding finding = AuditHarness.Of(audit, "AUD-100").Should().ContainSingle().Subject;

        finding.Severity.Should().Be(FindingSeverity.Information);

        // La sûreté n'est pas une préférence : une équipe peut décider qu'un constat ne l'inquiète pas,
        // pas qu'une transformation est prouvée.
        finding.Safety.Should().Be(RuleDefaults.For("AUD-100").Safety);
    }

    [Fact]
    public async Task Two_audits_of_the_same_document_report_the_same_findings_in_the_same_order()
    {
        SpecDocument document = await AuditHarness.ReadAsync(_root, "redundant.yaml");

        AuditResult first = AuditHarness.Audit(document);
        AuditResult second = AuditHarness.Audit(document);

        second.Findings.Select(finding => finding.RuleId + " " + finding.Location.Json)
            .Should().Equal(first.Findings.Select(finding => finding.RuleId + " " + finding.Location.Json));
    }

    [Fact]
    public async Task A_finding_offers_an_automatic_fix_only_when_it_is_proven_safe()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        audit.Findings
            .Where(finding => finding.AutofixAvailable)
            .Should().OnlyContain(finding => finding.Safety == SafetyClass.Safe
                && finding.StrictEquivalenceEstablished);
    }

    [Fact]
    public async Task The_audit_runs_without_a_candidate_document()
    {
        // L'examen porte sur un seul fichier, et c'est dans le sujet : il n'a aucune comparaison à faire.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        audit.Outcomes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_truncated_rule_says_what_it_left_out()
    {
        // Une borne de comparaison d'un seul candidat force la troncature, et la règle doit l'avouer
        // plutôt que rendre un résultat propre sur ce qu'elle n'a pas regardé.
        Ruleset ruleset = Ruleset.Default with { MaxNearDuplicateBucket = 1 };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        RuleOutcome outcome = AuditHarness.Outcome(audit, "AUD-104");

        outcome.State.Should().Be(RuleState.Truncated);
        outcome.Reason.Should().Contain("borne");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
