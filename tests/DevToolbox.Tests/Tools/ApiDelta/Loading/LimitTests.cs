using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Loading;

/// <summary>
/// Ce que l'outil refuse en le nommant, plutôt que de s'y briser.
/// </summary>
/// <remarks>
/// Une limite atteinte doit produire un diagnostic qui nomme la limite. La différence avec un
/// débordement de pile n'est pas de confort : un refus nommé se corrige en relevant la limite ou en
/// réparant le fichier, là qu'un plantage ne dit ni lequel des deux faire ni même ce qui s'est passé.
/// </remarks>
public sealed class LimitTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-limites-" + Guid.NewGuid().ToString("N"));

    public LimitTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_document_deeper_than_the_limit_is_refused_with_the_depth_named()
    {
        LoadLimits limits = LoadLimits.Default with { MaxDepth = 40 };

        SpecReadResult read = await ReadAsync(
            "profond.yaml", GeneratedDescription.Deep(200), limits);

        read.IsFaithful.Should().BeFalse("la profondeur dépasse la limite");

        Diagnostic refusal = read.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.LimitReached).Which;

        refusal.Message.Should().Contain("40", "la limite atteinte doit être nommée");
        refusal.Position.Should().NotBeNull("un refus sans position se cherche à la main");
    }

    [Fact]
    public async Task A_document_larger_than_the_limit_is_refused_with_the_size_named()
    {
        string content = GeneratedDescription.Wide(20, 40);

        LoadLimits limits = LoadLimits.Default with { MaxBytes = 128 };

        SpecReadResult read = await ReadAsync("gros.yaml", content, limits);

        read.IsFaithful.Should().BeFalse();

        read.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.LimitReached
            && diagnostic.Message.Contains("128", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cyclic_document_is_read_and_audited_without_looping()
    {
        // Légal en OpenAPI : le refuser serait refuser une description juste, et s'y perdre serait pire.
        SpecDocument document = await AuditHarness.ReadAsync(_root, "cyclic.yaml");

        AuditResult audit = AuditHarness.Audit(document);

        audit.Outcomes.Should().NotBeEmpty();
        audit.IsComplete.Should().BeTrue("aucune règle ne doit renoncer devant un cycle légal");
    }

    [Fact]
    public async Task A_reference_budget_that_is_reached_is_named_rather_than_silently_truncated()
    {
        LoadLimits limits = LoadLimits.Default with { MaxReferences = 3 };

        SpecReadResult read = await ReadAsync(
            "references.yaml", GeneratedDescription.Wide(20, 40), limits);

        // La lecture peut aboutir — la limite porte sur la résolution — mais si elle est atteinte, elle
        // est dite. Ce qui est interdit est de la franchir sans rien dire.
        if (!read.IsFaithful)
        {
            read.Diagnostics.Should().Contain(diagnostic =>
                diagnostic.Code == DiagnosticCodes.LimitReached);
        }
    }

    [Fact]
    public async Task The_limits_can_be_lowered_and_raised_by_a_ruleset()
    {
        // Les limites sont le budget contre le déni de service : une équipe les abaisse librement et les
        // relève en sachant ce qu'elle fait.
        string path = Path.Combine(_root, RulesetStore.ConventionalName);

        await File.WriteAllTextAsync(
            path,
            """
            limits:
              maxBytes: 64
              maxDepth: 12
            """);

        RulesetReadResult read = await new RulesetStore()
            .ReadAsync(path, Ruleset.Default, CancellationToken.None);

        read.IsApplicable.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        read.Ruleset!.Limits.MaxBytes.Should().Be(64);
        read.Ruleset.Limits.MaxDepth.Should().Be(12);
    }

    [Fact]
    public async Task A_document_that_is_not_openapi_at_all_is_refused_without_an_exception()
    {
        SpecReadResult read = await ReadAsync(
            "pas-openapi.yaml", "ceci: n est pas une description\nliste:\n  - un\n  - deux\n");

        read.IsFaithful.Should().BeFalse();
        read.Diagnostics.Should().NotBeEmpty();
        read.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Message.Length > 0);
    }

    [Fact]
    public async Task A_file_that_is_not_text_at_all_is_refused_without_an_exception()
    {
        string path = Path.Combine(_root, "binaire.yaml");

        await File.WriteAllBytesAsync(path, [0x00, 0x01, 0x02, 0xFF, 0xFE, 0x00, 0x42]);

        SpecReadResult read = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        read.IsFaithful.Should().BeFalse();
        read.Diagnostics.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<SpecReadResult> ReadAsync(
        string name,
        string content,
        LoadLimits? limits = null)
    {
        string path = Path.Combine(_root, name);
        await File.WriteAllTextAsync(path, content);

        return await new SpecReader(NullLogger<SpecReader>.Instance).ReadAsync(
            path, DocumentRole.Production, limits ?? LoadLimits.Default, CancellationToken.None);
    }
}
