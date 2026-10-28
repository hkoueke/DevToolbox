using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Loading;

/// <summary>
/// Ce qu'une écriture neuve doit préserver : le sens, exactement.
/// </summary>
/// <remarks>
/// L'émetteur canonique ne reproduit pas l'entrée — ce n'est pas son rôle — mais ce qu'il écrit doit dire
/// la même chose. L'essai est donc un aller-retour sémantique : écrire, relire, comparer. Si la
/// comparaison trouve la moindre différence, l'écriture a changé le contrat, et c'est précisément ce
/// qu'une copie « remise en forme » ne doit jamais faire.
/// </remarks>
public sealed class CanonicalEmissionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-emission-" + Guid.NewGuid().ToString("N"));

    public CanonicalEmissionTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("baseline-30.yaml")]
    [InlineData("baseline-30.json")]
    [InlineData("baseline-30.reformatted.yaml")]
    [InlineData("baseline-31.yaml")]
    [InlineData("candidate-additive.yaml")]
    [InlineData("candidate-breaking.yaml")]
    [InlineData("cyclic.yaml")]
    [InlineData("redundant.yaml")]
    [InlineData("secrets.yaml")]
    public async Task A_rewritten_document_says_exactly_what_it_said(string fileName)
    {
        SpecDocument original = await ReadAsync(fileName, CorpusLoader.Read(fileName));

        ReadOnlyMemory<byte> rewritten = new CanonicalEmitter().Emit(
            original.Root, CanonicalFormatting.Default);

        SpecDocument reread = await ReadAsync(
            "reecrit-" + fileName + ".yaml", System.Text.Encoding.UTF8.GetString(rewritten.Span));

        IReadOnlyList<Difference> differences = new DocumentComparer(new OpenApiMetamodel())
            .Compare(reread, original, CancellationToken.None)
            .Value;

        differences.Should().BeEmpty(
            string.Join(
                " · ",
                differences.Select(difference =>
                    difference.Location.Json + " " + difference.ChangeId)));
    }

    [Fact]
    public async Task A_quoted_response_code_stays_a_response_code()
    {
        // Le cas qui justifie à lui seul la quotation : écrit sans guillemets, « 200 » devient l'entier
        // 200, et la clé cesse d'être un code de réponse.
        SpecDocument original = await ReadAsync("codes.yaml", CorpusLoader.Read("baseline-30.yaml"));

        string rewritten = System.Text.Encoding.UTF8.GetString(
            new CanonicalEmitter().Emit(original.Root, CanonicalFormatting.Default).Span);

        rewritten.Should().Contain("'200':");
        rewritten.Should().NotContain("\n        200:");
    }

    [Fact]
    public async Task A_string_that_would_read_as_something_else_is_quoted()
    {
        SpecDocument original = await ReadAsync(
            "pieges.yaml",
            """
            openapi: 3.0.3
            info:
              title: Pièges
              version: 1.0.0
            paths: {}
            x-pieges:
              faux: 'no'
              vide: ''
              nombre: '1.5'
              date: '2026-10-17'
              rien: 'null'
              tilde: '~'
              deuxPoints: 'clef: valeur'
            """);

        string rewritten = System.Text.Encoding.UTF8.GetString(
            new CanonicalEmitter().Emit(original.Root, CanonicalFormatting.Default).Span);

        SpecDocument reread = await ReadAsync("pieges-relu.yaml", rewritten);

        // Chacune de ces valeurs est une chaîne, et doit le rester après un aller-retour.
        MappingNode traps = (MappingNode)PointerNavigator.Find(reread.Root, "/x-pieges")!;

        foreach (MappingEntry entry in traps.Entries)
        {
            ((ScalarNode)entry.Value).Resolved.Kind.Should().Be(
                ScalarValueKind.Text, "« {0} » est une chaîne", entry.Key.Text);
        }
    }

    [Fact]
    public async Task A_multiline_description_survives_being_rewritten()
    {
        SpecDocument original = await ReadAsync("multiligne.yaml", CorpusLoader.Read("redundant.yaml"));

        string rewritten = System.Text.Encoding.UTF8.GetString(
            new CanonicalEmitter().Emit(original.Root, CanonicalFormatting.Default).Span);

        SpecDocument reread = await ReadAsync("multiligne-relu.yaml", rewritten);

        ScalarNode description = (ScalarNode)PointerNavigator.Find(
            reread.Root,
            "/paths/~1initialiserDossiersPiecesEtDocuments/post/description")!;

        description.Text.Should().Contain("Endpoint obsol");
    }

    [Fact]
    public async Task The_default_preserves_the_source_order_and_sorting_stays_available()
    {
        // Le défaut a changé de sens : il conservait l'ordre du document seulement si on le demandait,
        // et il le conserve maintenant par défaut. Un fichier que cet outil écrit est destiné à
        // remplacer un document tenu à la main : le trier rendait sa relecture illisible — des milliers
        // de lignes d'écart pour une route ajoutée — au profit d'une reproductibilité que l'empreinte du
        // contenu établit déjà.
        SpecDocument original = await ReadAsync("ordre.yaml", CorpusLoader.Read("baseline-30.yaml"));

        CanonicalEmitter emitter = new();

        string byDefault = System.Text.Encoding.UTF8.GetString(
            emitter.Emit(original.Root, CanonicalFormatting.Default).Span);

        string sorted = System.Text.Encoding.UTF8.GetString(
            emitter.Emit(
                original.Root,
                CanonicalFormatting.Default with { KeyOrder = KeyOrderStyle.Canonical }).Span);

        // Par défaut, l'ordre suit le document, qui commence par « openapi ».
        byDefault.TrimStart().Should().StartWith("openapi:");

        // Le tri reste disponible : « components » y vient avant « openapi ».
        sorted.IndexOf("components:", StringComparison.Ordinal)
            .Should().BeLessThan(sorted.IndexOf("openapi:", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<SpecDocument> ReadAsync(string fileName, string content)
    {
        string path = Path.Combine(_root, fileName);
        await File.WriteAllTextAsync(path, content);

        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }
}
