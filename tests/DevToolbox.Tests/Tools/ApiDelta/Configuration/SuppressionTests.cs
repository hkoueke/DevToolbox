using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Configuration;

/// <summary>
/// Ce qu'une mise en sourdine tait, ce qu'elle ne tait pas, et ce qu'elle doit dire d'elle-même.
/// </summary>
public sealed class SuppressionTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-sourdines-" + Guid.NewGuid().ToString("N"));

    public SuppressionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void A_suppressed_finding_is_listed_rather_than_dropped()
    {
        // Une sourdine soustrait un constat au seuil d'échec, non au lecteur. C'est la seule façon de
        // rendre un fichier de sourdines vérifiable.
        SuppressionOutcome outcome = SuppressionApplier.Apply(
            [Finding("AUD-007", "/paths/~1un/post/requestBody")],
            [Motivated("AUD-007", "/paths/~1un/post/requestBody")],
            Today);

        outcome.Findings.Should().BeEmpty("le seuil ne doit plus le voir");

        SuppressedFinding silenced = outcome.Suppressed.Should().ContainSingle().Subject;

        silenced.Finding.RuleId.Should().Be("AUD-007");
        silenced.Suppression.Justification.Should().NotBeNullOrWhiteSpace();
        silenced.Suppression.Owner.Should().Be("equipe-documents");
    }

    [Fact]
    public void An_expired_suppression_reports_both_the_finding_and_the_expiry()
    {
        // Les deux, chaque exécution. Une sourdine qui vaudrait encore après sa date serait une sourdine
        // sans date.
        Suppression expired = Motivated("AUD-007", "/paths/~1un/post/requestBody") with
        {
            Expires = Today.AddDays(-1),
        };

        SuppressionOutcome outcome = SuppressionApplier.Apply(
            [Finding("AUD-007", "/paths/~1un/post/requestBody")], [expired], Today);

        outcome.Findings.Should().ContainSingle("le constat n'est pas tu");
        outcome.Suppressed.Should().BeEmpty();
        outcome.Expired.Should().ContainSingle().Which.Should().Be(expired);
    }

    [Fact]
    public void A_suppression_expiring_today_still_holds()
    {
        Suppression today = Motivated("AUD-007", "/paths/~1un/post/requestBody") with
        {
            Expires = Today,
        };

        SuppressionOutcome outcome = SuppressionApplier.Apply(
            [Finding("AUD-007", "/paths/~1un/post/requestBody")], [today], Today);

        outcome.Suppressed.Should().ContainSingle();
        outcome.Expired.Should().BeEmpty();
    }

    [Fact]
    public void A_suppression_that_matched_nothing_is_reported_as_such()
    {
        // D'ordinaire un emplacement corrigé ou renommé. La laisser en place sans rien dire est la façon
        // dont un fichier de sourdines devient de l'archéologie.
        SuppressionOutcome outcome = SuppressionApplier.Apply(
            [Finding("AUD-007", "/paths/~1un/post/requestBody")],
            [Motivated("AUD-007", "/paths/~1disparu/post/requestBody")],
            Today);

        outcome.Findings.Should().ContainSingle();
        outcome.Unmatched.Should().ContainSingle();
    }

    [Theory]
    [InlineData("/paths/*", "/paths/~1un", true)]
    [InlineData("/paths/*", "/paths/~1un/get", false)]
    [InlineData("/paths/**", "/paths/~1un/get/responses/400", true)]
    [InlineData("/paths/**/get", "/paths/~1un/get", true)]
    [InlineData("/paths/~1un/get", "/paths/~1un/get", true)]
    [InlineData("/paths/~1un/get", "/paths/~1deux/get", false)]
    [InlineData("/components/schemas/*", "/components/schemas/Ressource", true)]
    [InlineData("/**", "/paths/~1un/get", true)]
    public void A_glob_covers_one_segment_and_a_double_glob_covers_any_number(
        string pattern,
        string target,
        bool expected) =>
        PointerGlob.Matches(pattern, target).Should().Be(expected);

    [Fact]
    public void A_suppression_never_reaches_a_finding_of_another_rule()
    {
        SuppressionOutcome outcome = SuppressionApplier.Apply(
            [Finding("AUD-005", "/paths/~1un/get")],
            [Motivated("AUD-007", "/paths/**")],
            Today);

        outcome.Findings.Should().ContainSingle();
        outcome.Unmatched.Should().ContainSingle();
    }

    [Fact]
    public void An_unjustified_suppression_is_invalid_configuration()
    {
        // Refuser de démarrer est la seule réponse qui dise la vérité : l'honorer tairait un constat que
        // personne n'a motivé, l'ignorer rapporterait un constat que l'équipe croyait tu.
        IReadOnlyList<Diagnostic> refused = SuppressionValidator.Validate(
            [new Suppression("AUD-007", "/paths/~1un", null, "equipe")],
            Ruleset.Default,
            "apidelta.suppressions.yaml");

        refused.Should().ContainSingle()
            .Which.Message.Should().Contain("n'est pas justifiée")
            .And.Contain("apidelta.suppressions.yaml");
    }

    [Fact]
    public void An_unjustified_suppression_is_accepted_when_the_team_said_so()
    {
        Ruleset relaxed = Ruleset.Default with { RequireSuppressionJustification = false };

        SuppressionValidator.Validate(
                [new Suppression("AUD-007", "/paths/~1un", null, "equipe")],
                relaxed,
                "apidelta.suppressions.yaml")
            .Should().BeEmpty();
    }

    [Fact]
    public void A_suppression_without_an_owner_or_on_an_unknown_rule_is_invalid()
    {
        IReadOnlyList<Diagnostic> refused = SuppressionValidator.Validate(
            [
                new Suppression("AUD-007", "/paths/~1un", "parce que", null),
                new Suppression("AUD-999", "/paths/~1un", "parce que", "equipe"),
            ],
            Ruleset.Default,
            "apidelta.suppressions.yaml");

        refused.Select(diagnostic => diagnostic.Message)
            .Should().Contain(message => message.Contains("propriétaire", StringComparison.Ordinal))
            .And.Contain(message => message.Contains("n'existe pas", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_file_is_read_with_its_fields_and_its_expiry()
    {
        SuppressionReadResult read = await ReadAsync(
            """
            suppressions:
              - ruleId: AUD-007
                location: /paths/~1ajouterDocument/post/requestBody
                justification: Le portail historique envoie le corps sans en-tete de longueur.
                owner: equipe-documents
                expires: 2027-03-31
            """);

        read.IsApplicable.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        Suppression one = read.Suppressions.Should().ContainSingle().Subject;

        one.RuleId.Should().Be("AUD-007");
        one.Location.Should().Be("/paths/~1ajouterDocument/post/requestBody");
        one.Owner.Should().Be("equipe-documents");
        one.Expires.Should().Be(new DateOnly(2027, 3, 31));
    }

    [Fact]
    public async Task An_unreadable_expiry_is_an_error_and_not_a_perpetual_suppression()
    {
        // La traiter comme « pas d'échéance » rendrait une sourdine perpétuelle par faute de frappe, ce
        // qui est l'inverse de ce que son auteur avait écrit.
        SuppressionReadResult read = await ReadAsync(
            """
            suppressions:
              - ruleId: AUD-007
                location: /paths/~1un
                justification: parce que
                owner: equipe
                expires: bientot
            """);

        read.IsApplicable.Should().BeFalse();
        read.Diagnostics[0].Message.Should().Contain("bientot").And.Contain("date");
    }

    [Fact]
    public async Task An_unknown_suppression_field_is_named()
    {
        SuppressionReadResult read = await ReadAsync(
            """
            suppressions:
              - ruleId: AUD-007
                locaton: /paths/~1un
                justification: parce que
                owner: equipe
            """);

        read.IsApplicable.Should().BeFalse();
        read.Diagnostics[0].Message.Should().Contain("locaton");
    }

    [Fact]
    public async Task A_suppression_file_beside_the_description_is_discovered()
    {
        string description = Path.Combine(_root, "openapi.yaml");
        await File.WriteAllTextAsync(description, "openapi: 3.0.3");

        new SuppressionStore().Discover(description).Should().BeNull();

        string path = Path.Combine(_root, SuppressionStore.ConventionalName);
        await File.WriteAllTextAsync(path, "suppressions: []");

        new SuppressionStore().Discover(description).Should().Be(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Finding Finding(string ruleId, string pointer) =>
        new(
            ruleId,
            FindingSeverity.Warning,
            SafetyClass.ReviewRequired,
            "Quelque chose à dire.",
            Pointer(pointer),
            "Faire quelque chose.");

    /// <summary>Le pointeur d'une forme textuelle, segment par segment.</summary>
    private static SpecPointer Pointer(string json)
    {
        SpecPointer pointer = SpecPointer.Root;

        foreach (string segment in json.Trim('/').Split('/'))
        {
            pointer = pointer.Child(segment.Replace("~1", "/", StringComparison.Ordinal));
        }

        return pointer;
    }

    private static Suppression Motivated(string ruleId, string location) =>
        new(
            ruleId,
            location,
            "Le portail historique envoie le corps sans en-tete de longueur.",
            "equipe-documents");

    private async Task<SuppressionReadResult> ReadAsync(string content)
    {
        string path = Path.Combine(
            _root, Guid.NewGuid().ToString("N"), SuppressionStore.ConventionalName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);

        return await new SuppressionStore().ReadAsync(path, CancellationToken.None);
    }
}
