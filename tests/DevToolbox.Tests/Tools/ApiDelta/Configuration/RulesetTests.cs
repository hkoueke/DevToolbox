using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Configuration;

/// <summary>
/// Ce qu'un jeu de règles change, et ce qu'il refuse de laisser passer.
/// </summary>
public sealed class RulesetTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-jeu-regles-" + Guid.NewGuid().ToString("N"));

    public RulesetTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_raised_threshold_takes_effect_on_the_audit()
    {
        // Quatorze occurrences du même paramètre : signalées à deux, signalées à quatorze, taisues à
        // quinze. C'est un réglage de patience, et il doit se lire dans le résultat.
        Ruleset ruleset = await ReadAsync(
            """
            rules:
              AUD-100:
                minimumOccurrences: 15
            """);

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        AuditHarness.Of(audit, "AUD-100").Should().BeEmpty();

        AuditResult without = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        AuditHarness.Of(without, "AUD-100").Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_lowered_severity_takes_effect_without_touching_the_safety_class()
    {
        // La gravité dit combien une équipe s'en soucie ; la sûreté dit ce que l'outil a prouvé. Si un
        // fichier pouvait promouvoir la seconde, « prouvé sûr » cesserait d'être une preuve.
        Ruleset ruleset = await ReadAsync(
            """
            rules:
              AUD-100:
                severity: information
            """);

        Finding lowered = AuditHarness
            .Of(await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset), "AUD-100")
            .Should().NotBeEmpty().And.Subject.First();

        lowered.Severity.Should().Be(FindingSeverity.Information);
        lowered.Safety.Should().Be(SafetyClass.Safe, "la sûreté ne se configure pas");
        lowered.AutofixAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task A_disabled_rule_is_reported_as_disabled_rather_than_as_clean()
    {
        // Éteindre une règle est un choix visible. Une règle éteinte qui se présenterait comme n'ayant
        // rien trouvé ferait d'un rapport propre et d'un rapport aveugle la même chose.
        Ruleset ruleset = await ReadAsync(
            """
            rules:
              AUD-100:
                enabled: false
            """);

        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml", ruleset);

        AuditHarness.Of(audit, "AUD-100").Should().BeEmpty();
        AuditHarness.Outcome(audit, "AUD-100").State.Should().Be(RuleState.Disabled);
    }

    [Fact]
    public async Task An_unknown_rule_identifier_is_invalid_configuration()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            rules:
              AUD-999:
                enabled: false
            """);

        refused.Should().NotBeEmpty();
        refused[0].Code.Should().Be(ConfigurationCodes.Invalid);
        refused[0].Message.Should().Contain("AUD-999").And.Contain("n'est pas une règle connue");
    }

    [Fact]
    public async Task An_unknown_top_level_key_is_invalid_configuration()
    {
        // L'alternative est un fichier de configuration dont une section mal orthographiée ne fait rien
        // pendant un an.
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            standardHeadres:
              - X-serviceEmetteur
            """);

        refused.Should().ContainSingle()
            .Which.Message.Should().Contain("standardHeadres").And.Contain("n'est pas reconnue");
    }

    [Fact]
    public async Task An_unknown_rule_setting_and_a_bad_value_are_both_named()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            rules:
              AUD-100:
                severty: error
              AUD-101:
                severity: tres-grave
              AUD-102:
                minimumOccurrences: 1
            """);

        refused.Select(diagnostic => diagnostic.Message)
            .Should().Contain(message => message.Contains("severty", StringComparison.Ordinal))
            .And.Contain(message => message.Contains("tres-grave", StringComparison.Ordinal)
                || message.Contains("« severity » de AUD-101", StringComparison.Ordinal))
            .And.Contain(message => message.Contains(
                "une seule écriture n'est pas une répétition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_diagnostic_names_the_file_and_where_in_it()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            rules:
              AUD-999:
                enabled: false
            """);

        refused[0].Message.Should().Contain("apidelta.ruleset.yaml");
        refused[0].Position.Should().NotBeNull("un refus sans position se cherche à la main");
    }

    [Fact]
    public async Task A_protected_prefix_can_only_be_added_never_removed()
    {
        // Aucun jeu de règles ne rend une extension inconnue éligible à une suppression automatique.
        Ruleset ruleset = await ReadAsync(
            """
            protectedExtensions:
              - x-maison-
            """);

        ruleset.ProtectedExtensionPrefixes.Should().Contain("x-maison-")
            .And.Contain("x-sg-", "le préfixe livré survit à un fichier qui ne le cite pas");
    }

    [Fact]
    public async Task The_failure_threshold_and_the_growth_allowance_are_configurable()
    {
        Ruleset ruleset = await ReadAsync(
            """
            failOn: warning
            output:
              maxGrowthPercent: 5
            suppressions:
              requireJustification: false
            """);

        ruleset.Threshold.Should().Be(FailureThreshold.OnWarning);
        ruleset.MaxGrowthPercent.Should().Be(5);
        ruleset.RequireSuppressionJustification.Should().BeFalse();
    }

    [Fact]
    public async Task Every_section_the_contract_documents_is_read()
    {
        // Le contrat montre un fichier portant ces sections. Un lecteur qui les refuserait comme clés
        // inconnues rendrait le contrat faux ; un lecteur qui les ignorerait en silence le rendrait
        // pire.
        Ruleset ruleset = await ReadAsync(
            """
            limits:
              maxBytes: 1048576
              maxDepth: 50
              maxFindingsPerRule: 20
            references:
              allowLocal: true
              allowRemote: true
              remoteHosts: [ exemple.fr ]
              root: .
            canonicalFormatting:
              indent: 4
              lineEndings: crlf
              keyOrder: preserve
            versioning:
              enumValueAdded: minor
              fallbackBaseline: 1.0.0
            namingConventions:
              paths: kebab-case
              components: PascalCase
            environmentPlaceholders:
              pattern: '\$\{(?<name>[a-z.]+)\}'
              known: [ maison.host ]
            """);

        ruleset.Limits.MaxBytes.Should().Be(1_048_576);
        ruleset.Limits.MaxDepth.Should().Be(50);
        ruleset.MaxFindingsPerRule.Should().Be(20);
        ruleset.References.AllowRemote.Should().BeTrue();
        ruleset.References.RemoteHosts.Should().Equal("exemple.fr");
        ruleset.Formatting.Indent.Should().Be(4);
        ruleset.Formatting.LineEndings.Should().Be(LineEndingStyle.Crlf);
        ruleset.Formatting.KeyOrder.Should().Be(KeyOrderStyle.Preserve);
        ruleset.Versioning.EnumValueAdded.Should().Be(VersionImpact.Minor);
        ruleset.Versioning.FallbackBaseline.Should().Be("1.0.0");
        ruleset.Naming.PathSegments.Should().Be(NamingStyle.Kebab);
        ruleset.Naming.Components.Should().Be(NamingStyle.Pascal);
        ruleset.KnownPlaceholders.Should().Equal("maison.host");
    }

    [Fact]
    public async Task Remote_resolution_without_an_allow_list_is_invalid_and_not_an_open_door()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            references:
              allowRemote: true
            """);

        refused.Should().ContainSingle()
            .Which.Message.Should().Contain("remoteHosts").And.Contain("porte ouverte");
    }

    [Fact]
    public async Task A_section_value_that_is_not_what_it_should_be_names_its_section_and_its_key()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync(
            """
            limits:
              maxDepth: zero
            canonicalFormatting:
              keyOrder: alphabetique
            """);

        refused.Select(diagnostic => diagnostic.Message)
            .Should().Contain(message => message.Contains("limits.maxDepth", StringComparison.Ordinal))
            .And.Contain(message => message.Contains(
                "canonicalFormatting.keyOrder", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_file_that_is_not_yaml_is_refused_rather_than_ignored()
    {
        IReadOnlyList<Diagnostic> refused = await RefusedAsync("rules: [ ceci: n'est: pas: du: yaml");

        refused.Should().NotBeEmpty();
        refused.Should().OnlyContain(diagnostic => diagnostic.IsBlocking);
    }

    [Fact]
    public void The_shipped_defaults_are_a_working_configuration()
    {
        // L'outil sert sans aucun fichier : une équipe adopte un jeu de règles quand elle est en
        // désaccord avec l'une d'elles, non pour commencer à travailler.
        Ruleset shipped = Ruleset.Default;

        shipped.ProtectedExtensionPrefixes.Should().Contain("x-sg-");
        shipped.DeprecationPhrases.Should().Contain("Endpoint obsolète");
        shipped.EnvironmentPlaceholderPattern.Should().Contain("(?<name>");
        shipped.KnownPlaceholders.Should().Contain("catalog.host");
        shipped.StandardHeaders.Should().Contain("X-serviceEmetteur").And.Contain("X-correlationId");
        shipped.StandardResponses.Should().Contain("400").And.Contain("500");
        shipped.RequireSuppressionJustification.Should().BeTrue();
        shipped.Threshold.Should().Be(FailureThreshold.Never);
        shipped.MaxGrowthPercent.Should().Be(0, "une sortie plus grosse que l'entrée est refusée");
    }

    [Fact]
    public async Task With_no_configuration_file_at_all_the_audit_still_finds_what_matters()
    {
        AuditResult audit = await AuditHarness.AuditAsync(_root, "redundant.yaml");

        audit.Findings.Should().NotBeEmpty();
        audit.IsComplete.Should().BeTrue();
        audit.Outcomes.Should().OnlyContain(outcome => outcome.State != RuleState.Disabled);
    }

    [Fact]
    public async Task A_ruleset_beside_the_description_is_discovered_without_being_named()
    {
        string description = Path.Combine(_root, "openapi.yaml");
        await File.WriteAllTextAsync(description, "openapi: 3.0.3");

        new RulesetStore().Discover(description).Should().BeNull("rien n'est là encore");

        string path = Path.Combine(_root, RulesetStore.ConventionalName);
        await File.WriteAllTextAsync(path, "rules: {}");

        new RulesetStore().Discover(description).Should().Be(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Lit un jeu de règles qui doit être applicable.</summary>
    private async Task<Ruleset> ReadAsync(string content)
    {
        RulesetReadResult read = await ReadResultAsync(content);

        read.IsApplicable.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return read.Ruleset!;
    }

    /// <summary>Lit un jeu de règles qui doit être refusé, et rend ce qui l'explique.</summary>
    private async Task<IReadOnlyList<Diagnostic>> RefusedAsync(string content)
    {
        RulesetReadResult read = await ReadResultAsync(content);

        read.IsApplicable.Should().BeFalse("ce fichier ne doit pas être applicable");

        return read.Diagnostics;
    }

    private async Task<RulesetReadResult> ReadResultAsync(string content)
    {
        string path = Path.Combine(
            _root, Guid.NewGuid().ToString("N"), RulesetStore.ConventionalName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);

        return await new RulesetStore()
            .ReadAsync(path, Ruleset.Default, CancellationToken.None);
    }
}
