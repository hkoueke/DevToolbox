using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Audit.Redundancy;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Audit;

/// <summary>
/// Ce que les règles de répétition groupent, chiffrent, et refusent de proposer.
/// </summary>
public sealed class RedundancyRuleTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-redondance-" + Guid.NewGuid().ToString("N"));

    public RedundancyRuleTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_repeated_parameter_is_one_group_with_a_measured_saving()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        Finding finding = AuditHarness.Of(audit, "AUD-100").Should().ContainSingle().Subject;

        finding.Evidence!.Occurrences.Should().Be(14);
        finding.Evidence.Profile.Should().Be("strict");
        finding.Evidence.Fingerprint.Should().NotBeNullOrWhiteSpace();

        // Mesurée, non estimée à vue : la somme des occurrences moins la déclaration partagée et ses
        // références.
        finding.EstimatedBytesSaved.Should().BeGreaterThan(1000);
        finding.SuggestedComponentName.Should().Be("XServiceEmetteur");
        finding.AutofixAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task A_suggested_component_name_is_usable_as_a_name()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        audit.Findings
            .Where(finding => finding.SuggestedComponentName is not null)
            .Select(finding => finding.SuggestedComponentName!)
            .Should().OnlyContain(name => name.All(char.IsLetterOrDigit) && !char.IsDigit(name[0]));
    }

    [Fact]
    public async Task A_repeated_response_names_the_code_it_concerns()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> responses = AuditHarness.Of(audit, "AUD-102");

        responses.Should().HaveCount(2);
        responses.Select(finding => finding.SuggestedComponentName)
            .Should().BeEquivalentTo("Reponse200", "Reponse400");

        responses.Should().OnlyContain(finding => finding.Evidence!.Occurrences == 14);
    }

    [Fact]
    public async Task A_single_repeated_header_is_not_a_repeated_header_set()
    {
        // Un en-tête répété relève de la règle des paramètres. Le groupe d'en-têtes vise le cas où la
        // bonne réponse est de les remonter ensemble, et un groupe d'un seul n'en est pas un.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(audit, "AUD-101").Should().BeEmpty();
    }

    [Fact]
    public async Task A_repeated_header_set_is_reported_once_for_all_the_operations_that_carry_it()
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
                    - name: X-correlation
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
                    - name: X-correlation
                      in: header
                      required: true
                      schema:
                        type: string
                    - name: X-appelant
                      in: header
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        Finding finding = AuditHarness.Of(audit, "AUD-101").Should().ContainSingle().Subject;

        // L'ordre d'écriture des paramètres ne dit rien : les deux opérations déclarent le même groupe.
        finding.Locations.Should().HaveCount(2);

        // Remonter au niveau du chemin changerait la portée : le remède reste à relire.
        finding.Safety.Should().Be(SafetyClass.ReviewRequired);
        finding.AutofixAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Repeated_request_bodies_and_media_types_are_counted_apart()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-103");

        findings.Should().Contain(finding =>
            finding.Message.Contains("corps de requête", StringComparison.Ordinal));

        findings.Should().Contain(finding =>
            finding.Message.Contains("type de média", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Two_identical_schemas_under_different_names_are_never_safe_to_merge()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        Finding duplicate = AuditHarness.Of(audit, "AUD-104")
            .Should().ContainSingle(finding =>
                finding.Message.Contains("identiques", StringComparison.Ordinal)).Subject;

        duplicate.Message.Should().Contain("Ressource").And.Contain("RessourceBis");

        // Les engendreurs de clients tirent les noms de types des noms de schémas : fusionner deux noms
        // renomme un type chez l'appelant.
        duplicate.Safety.Should().Be(SafetyClass.ReviewRequired);
        duplicate.AutofixAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Two_schemas_that_merely_share_a_shape_are_not_called_near_duplicates()
    {
        // Deux objets qui ne diffèrent que par « properties » et « required » peuvent n'avoir rien en
        // commun. Les rapprocher ferait un constat que personne ne peut trancher.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        AuditHarness.Of(audit, "AUD-104").Should().BeEmpty();
    }

    [Fact]
    public async Task A_near_duplicate_names_the_fields_that_differ()
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
                Un:
                  type: object
                  required: [nom]
                  properties:
                    nom:
                      type: string
                      maxLength: 32
                Deux:
                  type: object
                  required: [nom]
                  properties:
                    nom:
                      type: string
                      maxLength: 64
            """);

        AuditResult audit = AuditHarness.Audit(document);

        Finding near = AuditHarness.Of(audit, "AUD-104").Should().ContainSingle().Subject;

        near.Severity.Should().Be(FindingSeverity.Information);
        near.Evidence!.DifferingFields.Should().Contain("properties");
        near.Evidence.Reasoning.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Identical_enumerations_are_reported_and_never_proposed_for_merging()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        Finding finding = AuditHarness.Of(audit, "AUD-106").Should().ContainSingle().Subject;

        finding.Safety.Should().Be(SafetyClass.ReviewRequired);
        finding.AutofixAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Components_that_differ_only_in_documentation_are_reported_with_the_differing_field()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        Finding alias = AuditHarness.Of(audit, "AUD-107").Should().ContainSingle().Subject;

        alias.Evidence!.DifferingFields.Should().Contain("description");
        alias.Evidence.Profile.Should().Be("contract");
        alias.Locations.Should().HaveCount(3);
        alias.Safety.Should().Be(SafetyClass.ReviewRequired);
    }

    [Fact]
    public async Task A_minimum_occurrence_setting_silences_a_group_below_it()
    {
        Ruleset ruleset = Ruleset.Default with
        {
            Rules = new Dictionary<string, RuleSetting>(StringComparer.Ordinal)
            {
                ["AUD-103"] = new RuleSetting(MinimumOccurrences: 20),
            },
        };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        AuditHarness.Of(audit, "AUD-103").Should().BeEmpty();
    }

    [Fact]
    public async Task Grouping_happens_under_the_profile_that_compares_documentation_too()
    {
        // Si la documentation était écartée du groupement, « identiques » cesserait de vouloir dire
        // « interchangeables », et extraire un composant commun jetterait une description sans le dire.
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
                      description: Le service appelant.
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
                      required: true
                      description: Autre explication.
                      schema:
                        type: string
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-100").Should().BeEmpty(
            "deux paramètres dont les descriptions diffèrent ne sont pas le même paramètre");
    }

    [Fact]
    public async Task An_extraction_that_would_gain_nothing_announces_no_saving()
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
                  responses:
                    '200':
                      description: Bon.
              /deux:
                get:
                  operationId: lireDeux
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        // Une réponse de deux lignes remplacée par deux références ne gagne rien, et le dire évite de
        // recommander un dérangement pour rien.
        AuditHarness.Of(audit, "AUD-102").Should().ContainSingle()
            .Which.EstimatedBytesSaved.Should().BeNull();
    }

    [Fact]
    public async Task The_break_even_point_is_reached_only_above_the_size_of_a_reference()
    {
        await Task.CompletedTask;

        Saving.BreakEven(200, 40).Should().Be(2);
        Saving.BreakEven(40, 40).Should().BeNull();
        // Quatorze occurrences de 200 octets, remplacées par une déclaration et quatorze références.
        Saving.Bytes(200, 14, 40).Should().Be((200 * 14) - (200 + (40 * 14)));
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
