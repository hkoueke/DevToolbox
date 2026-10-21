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
/// Ce que les règles de cohérence et de mise en forme voient, et ce qu'elles ne touchent pas.
/// </summary>
public sealed class ConsistencyRuleTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-coherence-" + Guid.NewGuid().ToString("N"));

    public ConsistencyRuleTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Response_codes_quoted_two_ways_are_reported_once_with_the_bare_ones_named()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        Finding finding = AuditHarness.Of(audit, "AUD-202").Should().ContainSingle().Subject;

        finding.Locations.Should().HaveCount(7);
        finding.Message.Should().Contain("nus");
        finding.Safety.Should().Be(SafetyClass.Safe);
    }

    [Fact]
    public async Task Component_names_are_not_counted_as_bare_response_codes()
    {
        // Sous « components », les clés de « responses » sont des noms de composants. Les compter comme
        // des codes nus inventerait un écart de quotation qui n'existe pas.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        AuditHarness.Of(audit, "AUD-202").Should().BeEmpty();
    }

    [Fact]
    public async Task Headers_differing_only_in_casing_are_reported_and_never_merged()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        Finding finding = AuditHarness.Of(audit, "AUD-203").Should().ContainSingle().Subject;

        finding.Evidence!.DifferingFields.Should().BeEquivalentTo("X-MARCHE", "X-Marche");

        // Une passerelle peut apparier l'une des deux écritures : l'outil ne les réunit pas.
        finding.Safety.Should().Be(SafetyClass.Unsafe);
        finding.AutofixAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task A_deprecation_phrase_without_the_flag_is_reported()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        Finding finding = AuditHarness.Of(audit, "AUD-205").Should().ContainSingle().Subject;

        finding.Message.Should().Contain("Endpoint obsolète");
        finding.Safety.Should().Be(SafetyClass.ReviewRequired);
    }

    [Fact]
    public async Task A_deprecation_phrase_with_the_flag_is_not_reported()
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
                  description: Endpoint obsolète, voir /deux.
                  deprecated: true
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-205").Should().BeEmpty();
    }

    [Fact]
    public async Task A_date_like_property_without_a_format_is_a_suggestion_that_carries_its_reasoning()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-206");

        findings.Should().HaveCount(2);
        findings.Should().OnlyContain(finding =>
            finding.Message.Contains("creeLe", StringComparison.Ordinal));

        // Déduit du nom, qui n'établit pas le contenu : le constat le dit au lieu de l'affirmer.
        findings.Should().OnlyContain(finding => finding.Evidence!.Reasoning != null);
    }

    [Fact]
    public async Task A_word_that_merely_ends_in_the_same_letters_is_not_a_date()
    {
        // « libelle » finit par les lettres de « le » sans être un horodatage. Sans la casse, la règle
        // signalerait un mot sur trois.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(audit, "AUD-206").Should().NotContain(finding =>
            finding.Message.Contains("libelle", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_description_repeated_everywhere_is_reported_once_with_its_locations()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-207");

        findings.Should().HaveCount(3);
        findings.Should().OnlyContain(finding => finding.Locations.Count == 14);
    }

    [Fact]
    public async Task Escaped_markup_and_a_left_over_placeholder_are_reported()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
              description: 'Voir &lt;b&gt;la documentation&lt;/b&gt;.'
            paths:
              /un:
                get:
                  operationId: lireUn
                  description: TODO
                  responses:
                    '200':
                      description: Bon.
            """);

        AuditResult audit = AuditHarness.Audit(document);

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-207");

        findings.Should().Contain(finding =>
            finding.Message.Contains("balisage échappé", StringComparison.Ordinal));

        findings.Should().Contain(finding =>
            finding.Message.Contains("substituant", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_required_property_that_does_not_exist_is_an_error()
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
                  required: [nom, absent]
                  properties:
                    nom:
                      type: string
            """);

        AuditResult audit = AuditHarness.Audit(document);

        Finding finding = AuditHarness.Of(audit, "AUD-208").Should().ContainSingle().Subject;

        finding.Severity.Should().Be(FindingSeverity.Error);
        finding.Message.Should().Contain("absent");
    }

    [Fact]
    public async Task An_array_without_an_item_schema_is_an_error()
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
                Liste:
                  type: array
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-208").Should().ContainSingle()
            .Which.Message.Should().Contain("ne dit pas ce qu'il contient");
    }

    [Fact]
    public async Task A_composed_schema_is_not_blamed_for_a_property_declared_elsewhere()
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
                Base:
                  type: object
                  properties:
                    id:
                      type: string
                Derive:
                  allOf:
                    - $ref: '#/components/schemas/Base'
                  required: [id]
                  properties:
                    nom:
                      type: string
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-208").Should().BeEmpty();
    }

    [Fact]
    public async Task Mixed_object_openness_names_the_schemas_that_say_nothing()
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
                Ferme:
                  type: object
                  additionalProperties: false
                  properties:
                    nom:
                      type: string
                Muet:
                  type: object
                  properties:
                    nom:
                      type: string
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-209").Should().ContainSingle()
            .Which.Location.Json.Should().Be("/components/schemas/Muet");
    }

    [Fact]
    public async Task A_document_that_never_declares_openness_is_left_alone()
    {
        // C'est un choix, et il est cohérent. La règle ne parle que du mélange.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.yaml");

        AuditHarness.Of(audit, "AUD-209").Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_extension_stays_opaque()
    {
        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            x-sg-passerelle:
              catalogue: essai
            paths: {}
            """);

        AuditResult audit = AuditHarness.Audit(document);

        AuditHarness.Of(audit, "AUD-210").Should().BeEmpty(
            "une extension dont la forme n'est pas déclarée ne se juge pas");
    }

    [Fact]
    public async Task A_known_extension_missing_a_declared_field_is_reported()
    {
        Ruleset ruleset = Ruleset.Default with
        {
            KnownExtensionFields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["x-sg-passerelle"] = ["catalogue", "cible"],
            },
        };

        SpecDocument document = await Written(
            """
            openapi: 3.0.3
            info:
              title: Essai
              version: 1.0.0
            x-sg-passerelle:
              catalogue: essai
            paths: {}
            """);

        AuditResult audit = AuditHarness.Audit(document, ruleset);

        AuditHarness.Of(audit, "AUD-210").Should().ContainSingle()
            .Which.Message.Should().Contain("cible");
    }

    [Fact]
    public async Task Formatting_departures_name_their_line()
    {
        string path = Path.Combine(_root, "forme.yaml");

        await File.WriteAllTextAsync(
            path,
            "openapi: 3.0.3\ninfo:\n   title: Essai   \n   version: 1.0.0\npaths: {}");

        SpecDocument document = await Read(path);

        AuditResult audit = AuditHarness.Audit(document);

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-200");

        findings.Should().Contain(finding =>
            finding.Message.Contains("espaces", StringComparison.Ordinal));

        findings.Should().Contain(finding =>
            finding.Message.Contains("multiple", StringComparison.Ordinal));

        findings.Should().Contain(finding =>
            finding.Message.Contains("ne se termine pas", StringComparison.Ordinal));

        findings.Should().OnlyContain(finding => finding.Message.StartsWith(
            "Ligne ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Formatting_is_not_evaluated_on_a_document_written_in_json()
    {
        // La mise en forme convenue décrit du YAML. Prétendre l'auditer sur du JSON serait rendre un
        // verdict sur une question qui ne se pose pas.
        AuditResult audit = await AuditHarness.AuditAsync(_root, "baseline-30.json");

        AuditHarness.Outcome(audit, "AUD-200").State.Should().Be(RuleState.NotEvaluated);
        audit.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Naming_conventions_say_nothing_until_one_is_declared()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(audit, "AUD-204").Should().BeEmpty();
    }

    [Fact]
    public async Task A_declared_naming_convention_is_applied()
    {
        Ruleset ruleset = Ruleset.Default with
        {
            Naming = new NamingPolicy(PathSegments: NamingStyle.Kebab, Components: NamingStyle.Pascal),
        };

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-204");

        findings.Should().Contain(finding =>
            finding.Message.Contains("initialiserDossiersPiecesEtDocuments", StringComparison.Ordinal));

        findings.Should().Contain(finding =>
            finding.Message.Contains("jamaisUtilise", StringComparison.Ordinal));

        // Renommer change ce que les clients engendrés voient : jamais automatiquement.
        findings.Should().OnlyContain(finding => !finding.AutofixAvailable);
    }

    [Fact]
    public async Task Unsorted_keys_are_reported_without_claiming_what_the_keys_are()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        IReadOnlyList<Finding> findings = AuditHarness.Of(audit, "AUD-201");

        findings.Select(finding => finding.Location.Json).Should().BeEquivalentTo("/paths", "/components");
        findings.Should().OnlyContain(finding => finding.Safety == SafetyClass.Safe);
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
