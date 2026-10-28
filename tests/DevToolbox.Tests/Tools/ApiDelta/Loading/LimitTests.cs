using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
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
        // Le budget porte sur la résolution, et c'est donc la résolution qu'il faut éprouver. La version
        // précédente de cet essai lisait le document et plaçait son assertion sous « si la lecture a
        // échoué » : la lecture ne résolvant rien, la condition n'était jamais vraie, l'assertion ne
        // s'exécutait jamais, et l'essai restait vert alors que « limits.maxReferences » n'était appliqué
        // nulle part dans le dépôt.
        SpecReadResult read = await ReadAsync("references.yaml", GeneratedDescription.Wide(20, 40));

        read.IsFaithful.Should().BeTrue("le document est juste : c'est son nombre de références qui est en cause");

        List<Diagnostic> found = [];

        new ReferenceGraph().Resolve(
            read.Document!,
            ReferencePolicy.Closed,
            LoadLimits.Default with { MaxReferences = 3 },
            found);

        Diagnostic refusal = found.Should().ContainSingle(diagnostic =>
            diagnostic.Code == DiagnosticCodes.LimitReached).Which;

        refusal.Message.Should().Contain("3", "la limite atteinte doit être nommée");
        refusal.Message.Should().Contain("maxReferences", "un refus se corrige si l'on sait quoi régler");
        refusal.Position.Should().NotBeNull("un refus sans position se cherche à la main");
    }

    [Fact]
    public async Task A_reference_budget_that_is_not_reached_says_nothing()
    {
        // L'autre moitié, sans laquelle la première ne prouve rien : un garde-fou qui se déclenche
        // toujours est aussi inutilisable qu'un garde-fou qui ne se déclenche jamais.
        SpecReadResult read = await ReadAsync("references.yaml", GeneratedDescription.Wide(20, 40));

        List<Diagnostic> found = [];

        new ReferenceGraph().Resolve(
            read.Document!, ReferencePolicy.Closed, LoadLimits.Default, found);

        found.Should().NotContain(diagnostic => diagnostic.Code == DiagnosticCodes.LimitReached);
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

    [Fact]
    public async Task An_alias_budget_that_is_reached_is_named()
    {
        // Ce budget mordait déjà, mais rien ne le prouvait : c'était le seul garde-fou appliqué du
        // dépôt dont aucun essai n'établissait qu'il arrête ce qu'il prétend arrêter. Un garde-fou que
        // personne n'éprouve est un garde-fou qu'un remaniement enlève sans que rien ne rougisse.
        LoadLimits limits = LoadLimits.Default with { MaxAliasExpansions = 0 };

        SpecReadResult read = await ReadAsync("alias.yaml", WithAnAlias, limits);

        read.IsFaithful.Should().BeFalse();

        Diagnostic refusal = read.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.LimitReached).Which;

        refusal.Message.Should().Contain("alias", "le refus doit dire de quel budget il s'agit");
        refusal.Message.Should().Contain("0", "la limite atteinte doit être nommée");
    }

    [Fact]
    public async Task An_alias_under_the_budget_is_still_refused_for_being_an_alias()
    {
        // Les deux refus sont distincts et ne doivent pas se confondre : sous le budget, l'outil refuse
        // l'alias parce qu'il ne réécrirait pas ce qu'il n'a pas lu — et non parce qu'une limite est
        // atteinte.
        SpecReadResult read = await ReadAsync("alias.yaml", WithAnAlias);

        read.IsFaithful.Should().BeFalse();

        read.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.ParseFailed
            && diagnostic.Message.Contains("alias", StringComparison.Ordinal));

        read.Diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.LimitReached);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Une description qui emploie un alias YAML, seule forme qui met le budget d'expansions en jeu.
    /// </summary>
    private const string WithAnAlias = """
        openapi: 3.0.3
        info:
          title: Description a alias
          version: 1.0.0
        paths:
          /chose:
            get:
              responses: &reponses
                '200':
                  description: Bien
          /autre:
            get:
              responses: *reponses
        """;

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
