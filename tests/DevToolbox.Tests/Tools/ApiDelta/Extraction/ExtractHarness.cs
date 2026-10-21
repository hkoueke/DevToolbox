using DevToolbox.Domain.Results;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevToolbox.Tests.Tools.ApiDelta.Extraction;

/// <summary>
/// Monte l'extraction comme la racine de composition la monte : catalogue, constructeur, émetteur,
/// vérificateur.
/// </summary>
internal static class ExtractHarness
{
    /// <summary>Lit un fichier du corpus.</summary>
    internal static async Task<SpecDocument> CorpusAsync(string root, string fileName)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + "-" + fileName);
        await File.WriteAllTextAsync(path, CorpusLoader.Read(fileName));

        return await ReadAsync(path);
    }

    /// <summary>Lit une description écrite pour l'essai.</summary>
    internal static async Task<SpecDocument> WrittenAsync(string root, string content)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".yaml");
        await File.WriteAllTextAsync(path, content);

        return await ReadAsync(path);
    }

    /// <summary>Le catalogue des opérations sélectionnables.</summary>
    internal static IReadOnlyList<SelectableOperation> Catalogue(SpecDocument document) =>
        new OperationCatalog().List(document);

    /// <summary>Monte le constructeur.</summary>
    internal static ExtractBuilder Builder() =>
        new(new DependencyResolver(new OpenApiMetamodel()));

    /// <summary>Monte le vérificateur.</summary>
    internal static ExtractVerifier Verifier()
    {
        OpenApiMetamodel metamodel = new();

        return new ExtractVerifier(
            new SpecReader(NullLogger<SpecReader>.Instance),
            new OpenApiSemanticAdapter(),
            new ReferenceGraph(),
            new Fingerprinter(new Canonicalizer(metamodel)));
    }

    /// <summary>
    /// Construit, marque, émet et vérifie — la chaîne entière, comme l'étape la monte.
    /// </summary>
    /// <param name="document">La source.</param>
    /// <param name="selectors">Les opérations retenues.</param>
    /// <param name="builder">Un constructeur de remplacement, pour éprouver un refus.</param>
    /// <param name="stamp">Appose la marque de provenance.</param>
    /// <returns>Le résultat, ou l'échec du constructeur.</returns>
    internal static Result<ExtractResult> Extract(
        SpecDocument document,
        IReadOnlyList<OperationSelector> selectors,
        IExtractBuilder? builder = null,
        bool stamp = true)
    {
        ExtractRequest request = new(selectors, stamp);

        Result<BuiltExtract> built = (builder ?? Builder())
            .Build(document, request, CancellationToken.None);

        if (built.IsFailure)
        {
            return Result.Fail<ExtractResult>(built.Failure!);
        }

        ExtractProvenance provenance = new(
            document.Path,
            document.ContentHash,
            new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero),
            selectors);

        SpecNode root = stamp
            ? ProvenanceStamp.Apply(built.Value.Document, provenance)
            : built.Value.Document;

        ReadOnlyMemory<byte> serialized =
            new CanonicalEmitter().Emit(root, CanonicalFormatting.Default);

        ExtractVerification verification = Verifier()
            .Verify(serialized, document, selectors, Ruleset.Default);

        return Result.Success(new ExtractResult(
            root,
            verification.Established ? serialized : ReadOnlyMemory<byte>.Empty,
            built.Value.Closure,
            built.Value.CarriedTags,
            built.Value.InheritedContext,
            ExtensionCarryOver.Dangling(root, selectors),
            provenance,
            verification));
    }

    /// <summary>L'extrait relu comme une description, pour l'inspecter.</summary>
    internal static SpecDocument Reread(ExtractResult result)
    {
        SpecReadResult read = new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadBytes(result.Serialized, "extrait", DocumentRole.Production, LoadLimits.Default);

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return read.Document!;
    }

    /// <summary>Le texte de l'extrait émis.</summary>
    internal static string Text(ExtractResult result) =>
        System.Text.Encoding.UTF8.GetString(result.Serialized.Span);

    private static async Task<SpecDocument> ReadAsync(string path)
    {
        SpecReadResult read = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return read.Document!;
    }
}
