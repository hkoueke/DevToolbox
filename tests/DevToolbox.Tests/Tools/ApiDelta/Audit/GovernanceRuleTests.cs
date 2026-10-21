using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Audit;

/// <summary>
/// Ce que les règles de gouvernance signalent, sans jamais y toucher.
/// </summary>
public sealed class GovernanceRuleTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-gouvernance-" + Guid.NewGuid().ToString("N"));

    public GovernanceRuleTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_departure_from_the_standard_is_one_finding_per_operation()
    {
        // Un constat par manquement ferait quatre lignes par opération pour une seule décision : adopter
        // le standard, ou le changer.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-300");

        findings.Should().HaveCount(7);
        findings.Select(finding => finding.Location.Json).Should().OnlyHaveUniqueItems();
        findings.Should().OnlyContain(finding =>
            finding.Message.Contains("en-têtes absents", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_standard_the_ruleset_does_not_declare_is_never_demanded()
    {
        Ruleset ruleset = Ruleset.Default with { StandardHeaders = [], StandardResponses = [] };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml", ruleset);

        AuditHarness.Of(audit, "AUD-300").Should().BeEmpty();
    }

    [Fact]
    public async Task An_operation_without_a_tag_or_without_security_is_named()
    {
        Ruleset ruleset = Ruleset.Default with { StandardHeaders = [], StandardResponses = [] };

        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths:
              /un:
                get:
                  operationId: lireUn
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document, ruleset);

        Finding finding = AuditHarness.Of(audit, "AUD-300").Should().ContainSingle().Subject;

        finding.Message.Should().Contain("aucune étiquette").And.Contain("aucune sécurité");
    }

    [Fact]
    public async Task The_same_header_required_here_and_optional_there_is_reported()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths:
              /un:
                get:
                  operationId: lireUn
                  parameters:
                    - name: X-appelant
                      in: header
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: Bon.
              /deux:
                get:
                  operationId: lireDeux
                  parameters:
                    - name: X-appelant
                      in: header
                      required: false
                      schema:
                        type: string
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        Finding finding = AuditHarness.Of(audit, "AUD-301").Should().ContainSingle().Subject;

        finding.Message.Should().Contain("X-appelant");
        finding.Locations.Should().HaveCount(2);
        finding.Safety.Should().Be(SafetyClass.Unsafe);
    }

    [Fact]
    public async Task An_empty_security_requirement_is_reported_as_cancelling_the_document_security()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        Finding finding = AuditHarness.Of(audit, "AUD-302").Should().ContainSingle().Subject;

        finding.Message.Should().Contain("annule");
        finding.Location.Json.Should().Be("/paths/~1dossiers/get");
    }

    [Fact]
    public async Task A_server_url_without_a_placeholder_is_reported_as_a_hard_coded_environment()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            servers:
              - url: https://recette.exemple.fr/api
            paths: {}
            """);

        AuditResult audit = AuditHarness.Audit(document);

        Finding finding = AuditHarness.Of(audit, "AUD-303").Should().ContainSingle().Subject;

        finding.Severity.Should().Be(FindingSeverity.Warning);
        finding.Message.Should().Contain("aucun substituant");
    }

    [Fact]
    public async Task An_unknown_placeholder_is_an_error_because_nothing_will_replace_it()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            servers:
              - url: https://$(catalog.inconnu)/api
            paths: {}
            """);

        AuditResult audit = AuditHarness.Audit(document);

        Finding finding = AuditHarness.Of(audit, "AUD-303").Should().ContainSingle().Subject;

        finding.Severity.Should().Be(FindingSeverity.Error);
        finding.Message.Should().Contain("catalog.inconnu");
    }

    [Fact]
    public async Task A_known_placeholder_is_accepted()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            servers:
              - url: https://$(catalog.host)/api
            paths: {}
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-303").Should().BeEmpty();
    }

    [Fact]
    public async Task A_vendor_extension_written_two_ways_is_reported_without_being_altered()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths:
              /un:
                get:
                  operationId: lireUn
                  x-sg-passerelle:
                    catalogue: essai
                    cible: https://exemple.fr
                  responses:
                    '200':
                      description: Bon.
              /deux:
                get:
                  operationId: lireDeux
                  x-sg-passerelle:
                    catalogue: essai
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        Finding finding = AuditHarness.Of(audit, "AUD-304").Should().ContainSingle().Subject;

        finding.Message.Should().Contain("x-sg-passerelle").And.Contain("2 formes");
        finding.Evidence!.DifferingFields.Should().Contain("cible");
        finding.Safety.Should().Be(SafetyClass.Unsafe);
    }

    [Fact]
    public async Task An_extension_outside_the_protected_prefixes_is_not_this_rules_business()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths:
              /un:
                get:
                  operationId: lireUn
                  x-autre:
                    a: 1
                  responses:
                    '200':
                      description: Bon.
              /deux:
                get:
                  operationId: lireDeux
                  x-autre:
                    b: 2
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-304").Should().BeEmpty();
    }

    [Fact]
    public async Task An_unused_tag_and_an_unnamed_security_scheme_are_reported()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-305");

        findings.Select(finding => finding.Message)
            .Should().Contain(message => message.Contains("Jamais", StringComparison.Ordinal))
            .And.Contain(message => message.Contains("jamaisUtilise", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_security_scheme_a_requirement_names_is_not_dead_metadata()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(audit, "AUD-305").Should().NotContain(finding =>
            finding.Message.Contains("bearerAuth", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<SpecDocument> Written(string content)
    {
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".yaml");
        await File.WriteAllTextAsync(path, content);

        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }
}
