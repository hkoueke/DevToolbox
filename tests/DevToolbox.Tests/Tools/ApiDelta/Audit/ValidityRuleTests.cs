using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Audit;

/// <summary>
/// Ce que les règles de validité trouvent, et ce qu'elles se gardent d'affirmer.
/// </summary>
public sealed class ValidityRuleTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-validite-" + Guid.NewGuid().ToString("N"));

    public ValidityRuleTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Unreachable_components_are_named_and_their_bytes_counted()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> unreachable = AuditHarness.Of(audit, "AUD-003");

        unreachable.Select(finding => finding.Location.Json).Should().BeEquivalentTo(
                "/components/parameters/JamaisReference",
                "/components/schemas/Inaccessible",
                "/components/schemas/InaccessibleAussi",
                "/components/schemas/RessourceBis",
                "/components/securitySchemes/jamaisUtilise");

        unreachable.Should().OnlyContain(finding => finding.EstimatedBytesSaved > 0);
    }

    [Fact]
    public async Task A_security_scheme_a_requirement_names_is_reachable_though_no_reference_points_to_it()
    {
        // Une exigence de sécurité nomme un mécanisme par sa clé, non par une référence. Le tenir pour
        // inaccessible proposerait de retirer l'authentification de tout le document.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(audit, "AUD-003")
            .Should().NotContain(finding => finding.Location.Json.EndsWith(
                "bearerAuth", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Two_components_that_only_reference_each_other_are_both_unreachable()
    {
        // Chacun a un référent, et aucun des deux ne sert : seule une accessibilité calculée depuis les
        // chemins les trouve.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(audit, "AUD-003").Select(finding => finding.Location.Json)
            .Should().Contain("/components/schemas/Inaccessible")
            .And.Contain("/components/schemas/InaccessibleAussi");
    }

    [Fact]
    public async Task A_component_a_keep_pattern_matches_is_never_proposed_for_removal()
    {
        Ruleset ruleset = Ruleset.Default with { KeepComponentPatterns = ["Inaccessible*"] };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        AuditHarness.Of(audit, "AUD-003").Select(finding => finding.Location.Json)
            .Should().NotContain("/components/schemas/Inaccessible")
            .And.NotContain("/components/schemas/InaccessibleAussi");
    }

    [Fact]
    public async Task An_unresolved_reference_is_reported_with_every_place_that_uses_it()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "broken-unresolved-ref.yaml");

        Finding dangling = AuditHarness.Of(audit, "AUD-002").Should().ContainSingle().Subject;

        dangling.Severity.Should().Be(FindingSeverity.Error);
        dangling.Message.Should().Contain("Dossier");

        // Trois emplacements l'emploient : un seul défaut, mais trois corrections.
        dangling.Locations.Should().HaveCount(3);
        dangling.Evidence!.Occurrences.Should().Be(3);
    }

    [Fact]
    public async Task A_request_body_that_does_not_state_its_requiredness_is_reported()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> silent = AuditHarness.Of(audit, "AUD-007");

        silent.Should().HaveCount(2);

        // Le remède change ce que la description promet : il ne peut pas être automatique.
        silent.Should().OnlyContain(finding => finding.Safety == SafetyClass.ReviewRequired
            && !finding.AutofixAvailable);
    }

    [Fact]
    public async Task An_empty_field_is_reported_as_present_and_empty()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(audit, "AUD-010").Should().ContainSingle()
            .Which.Location.Json.Should().Be("/externalDocs/url");
    }

    [Fact]
    public async Task Missing_and_duplicated_operation_identifiers_do_not_carry_the_same_severity()
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
                  operationId: lire
                  responses:
                    '200':
                      description: Bon.
              /deux:
                get:
                  operationId: lire
                  responses:
                    '200':
                      description: Bon.
              /trois:
                get:
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-005");

        findings.Should().HaveCount(2);

        findings.Should().ContainSingle(finding => finding.Severity == FindingSeverity.Error)
            .Which.Locations.Should().HaveCount(2);

        findings.Should().ContainSingle(finding => finding.Severity == FindingSeverity.Warning);
    }

    [Fact]
    public async Task A_path_placeholder_without_a_parameter_is_reported_in_both_directions()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths:
              /documents/{id}:
                get:
                  operationId: lire
                  parameters:
                    - name: autre
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-004");

        findings.Should().HaveCount(2);
        findings.Should().Contain(finding => finding.Message.Contains("{id}", StringComparison.Ordinal));
        findings.Should().Contain(finding => finding.Message.Contains("autre", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_path_parameter_that_is_not_required_is_reported()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths:
              /documents/{id}:
                get:
                  operationId: lire
                  parameters:
                    - name: id
                      in: path
                      schema:
                        type: string
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-004").Should().ContainSingle()
            .Which.Message.Should().Contain("obligatoire");
    }

    [Fact]
    public async Task An_operation_without_responses_is_reported()
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
                  operationId: lire
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-006").Should().Contain(finding =>
            finding.Message.Contains("aucune réponse", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_error_response_the_neighbours_declare_is_a_suggestion_with_its_reasoning()
    {
        // Les réponses que le jeu de règles tient pour standard relèvent de la gouvernance. Ce qui reste
        // ici est l'usage que le document s'est donné lui-même, et c'est une déduction.
        Ruleset ruleset = Ruleset.Default with { StandardResponses = [] };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml", ruleset);

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-006");

        findings.Should().NotBeEmpty();
        findings.Should().OnlyContain(finding => finding.Evidence!.Reasoning != null);
    }

    [Fact]
    public async Task A_nullability_construct_from_the_other_version_is_reported()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.1.0
            info:
              title: Essai
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Chose:
                  type: object
                  properties:
                    nom:
                      type: string
                      nullable: true
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-008").Should().ContainSingle()
            .Which.Message.Should().Contain("nullable");
    }

    [Fact]
    public async Task A_format_that_does_not_belong_to_its_type_is_reported()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Chose:
                  type: object
                  properties:
                    quand:
                      type: integer
                      format: date-time
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-009").Should().ContainSingle()
            .Which.Message.Should().Contain("date-time");
    }

    [Fact]
    public async Task An_unknown_format_is_left_alone()
    {
        // OpenAPI autorise explicitement à inventer un format : en signaler un serait signaler une
        // pratique permise.
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Chose:
                  type: object
                  properties:
                    siret:
                      type: string
                      format: siret
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-009").Should().BeEmpty();
    }

    [Fact]
    public async Task The_structure_rule_says_it_was_not_evaluated_when_nothing_validated_the_structure()
    {
        SpecDocument document = await AuditHarness.ReadAsync(_root, "baseline-30.yaml");

        AuditResult audit = Without(document);

        AuditHarness.Outcome(audit, "AUD-001").State.Should().Be(RuleState.NotEvaluated);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Examine un document sans lui donner ni graphe ni verdicts.</summary>
    private static AuditResult Without(SpecDocument document)
    {
        OpenApiMetamodel metamodel = new();

        return new AuditEngine(
                AuditHarness.Wired(),
                metamodel,
                new Fingerprinter(new Canonicalizer(metamodel)))
            .Run(new AuditRequest(document, Ruleset.Default), CancellationToken.None);
    }

    /// <summary>Lit une description écrite pour l'essai.</summary>
    private async Task<SpecDocument> Written(string content)
    {
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".yaml");
        await File.WriteAllTextAsync(path, content);

        return await Read(path);
    }

    private static async Task<SpecDocument> Read(string path)
    {
        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }
}
