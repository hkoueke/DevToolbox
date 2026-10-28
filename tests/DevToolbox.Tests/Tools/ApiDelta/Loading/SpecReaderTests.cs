using System.Text;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Loading;

/// <summary>
/// Ce que la lecture établit, et ce qu'elle refuse d'établir.
/// </summary>
public sealed class SpecReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-lecture-" + Guid.NewGuid().ToString("N"));

    public SpecReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Both_halves_of_a_duplicate_key_are_reported_and_nothing_is_read()
    {
        SpecReadResult result = await ReadAsync("broken-duplicate-key.yaml");

        result.IsFaithful.Should().BeFalse("une description mal lue ne doit pas exister");

        IReadOnlyList<Diagnostic> duplicates =
        [
            .. result.Diagnostics.Where(diagnostic =>
                string.Equals(diagnostic.Code, DiagnosticCodes.DuplicateKey, StringComparison.Ordinal)),
        ];

        // Le fichier en contient deux : la réponse '200' et la clé « properties ». Signaler la première
        // puis s'arrêter laisserait croire qu'il n'y en avait qu'une.
        duplicates.Should().HaveCount(2);

        foreach (Diagnostic duplicate in duplicates)
        {
            duplicate.Position.Should().NotBeNull("un développeur cherche une ligne, pas un octet");
            duplicate.Related.Should().ContainSingle("les deux moitiés comptent, non la seconde seule");
            duplicate.Related[0].Line.Should().BeLessThan(duplicate.Position.Line);
        }
    }

    [Fact]
    public async Task A_duplicate_property_in_json_is_reported_too()
    {
        string path = Write(
            "doublon.json",
            """
            {
              "openapi": "3.0.3",
              "info": { "title": "Doublon", "version": "1.0.0" },
              "paths": {},
              "paths": {}
            }
            """);

        SpecReadResult result = await ReadAsync(path, fromCorpus: false);

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.DuplicateKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unsupported_version_is_refused_with_the_range_that_is_supported()
    {
        SpecReadResult result = await ReadAsync("broken-version.yaml");

        result.IsFaithful.Should().BeFalse();

        Diagnostic refusal = result.Diagnostics.Single(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.UnsupportedVersion, StringComparison.Ordinal));

        refusal.Message.Should().Contain("3.0.x").And.Contain("3.1.x");
        refusal.Position.Should().NotBeNull();
    }

    [Fact]
    public async Task A_description_without_a_declared_version_is_refused_rather_than_guessed_at()
    {
        string path = Write("sans-version.yaml", "info:\n  title: Sans version\n  version: 1.0.0\npaths: {}\n");

        SpecReadResult result = await ReadAsync(path, fromCorpus: false);

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.MissingVersion, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_parse_error_carries_its_line_and_column()
    {
        // Un scalaire entre guillemets jamais refermé : l'analyseur sait dire où.
        string path = Write("casse.yaml", "openapi: 3.0.3\ninfo:\n  title: \"non fermé\npaths: {}\n");

        SpecReadResult result = await ReadAsync(path, fromCorpus: false);

        result.IsFaithful.Should().BeFalse();

        Diagnostic failure = result.Diagnostics.First(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.ParseFailed, StringComparison.Ordinal));

        failure.Position.Should().NotBeNull();
        failure.Position.Line.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task A_parser_that_throws_something_unexpected_still_produces_a_refusal()
    {
        // Cette entrée fait sortir l'analyseur par une exception générique, sans position. L'entrée étant
        // par nature hostile, elle doit devenir un refus et non une panne.
        string path = Write("casse-autrement.yaml", "openapi: 3.0.3\ninfo:\n  title: [non fermé\npaths: {}\n");

        SpecReadResult result = await ReadAsync(path, fromCorpus: false);

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.ParseFailed, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Legal_cycles_between_schemas_neither_fail_nor_loop()
    {
        SpecReadResult result = await ReadAsync("cyclic.yaml");

        result.IsFaithful.Should().BeTrue("un cycle entre schémas est légal");
        result.Diagnostics.Should().NotContain(diagnostic => diagnostic.IsBlocking);
    }

    [Fact]
    public async Task The_format_is_read_from_the_content_not_from_the_extension()
    {
        // Le même document, nommé .yaml mais écrit en JSON.
        string path = Write("menteur.yaml", CorpusLoader.Read("baseline-30.json"));

        SpecReadResult result = await ReadAsync(path, fromCorpus: false);

        result.IsFaithful.Should().BeTrue();
        result.Document!.Format.Should().Be(SpecFormat.Json);
    }

    [Fact]
    public async Task An_ambiguous_scalar_is_reported_without_being_decided()
    {
        string path = Write(
            "ambigu.yaml",
            """
            openapi: 3.0.3
            info:
              title: Ambigu
              version: 1.0.0
            paths: {}
            x-drapeaux:
              actif: no
              version: 0644
            """);

        SpecReadResult result = await ReadAsync(path, fromCorpus: false);

        // Lisible : le document n'est pas refusé. Mais l'auteur n'a peut-être pas écrit ce qu'il croyait.
        result.IsFaithful.Should().BeTrue();
        result.Diagnostics.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Diagnostics.Should().OnlyContain(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.AmbiguousScalar, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Html_escaped_text_is_reported_and_left_exactly_as_written()
    {
        string path = Write(
            "encode.yaml",
            """
            openapi: 3.0.3
            info:
              title: Encodé
              version: 1.0.0
              description: Utiliser &gt; pour citer
            paths: {}
            """);

        SpecReadResult result = await ReadAsync(path, fromCorpus: false);

        result.IsFaithful.Should().BeTrue();
        result.Diagnostics.Should().Contain(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.SuspiciousEncoding, StringComparison.Ordinal));

        // Signalé, non corrigé : décoder changerait ce que la description dit.
        System.Text.Encoding.UTF8.GetString(result.Document!.Source.Span).Should().Contain("&gt;");
    }

    [Fact]
    public async Task Nesting_past_the_limit_is_refused_by_naming_the_limit()
    {
        string path = Write("profond.yaml", CorpusLoader.GenerateDeep(depth: 60));

        SpecReadResult result = await ReadAsync(path, fromCorpus: false, limits: new LoadLimits(MaxDepth: 20));

        result.IsFaithful.Should().BeFalse();

        Diagnostic refusal = result.Diagnostics.Single(diagnostic =>
            string.Equals(diagnostic.Code, DiagnosticCodes.LimitReached, StringComparison.Ordinal));

        refusal.Message.Should().Contain("20");
    }

    [Fact]
    public async Task A_file_past_the_size_limit_is_refused_by_naming_the_limit()
    {
        string path = Write("volumineux.yaml", CorpusLoader.Read("baseline-30.yaml"));

        SpecReadResult result = await ReadAsync(path, fromCorpus: false, limits: new LoadLimits(MaxBytes: 128));

        result.IsFaithful.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Message.Contains("128", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("baseline-30.yaml")]
    [InlineData("baseline-30.reformatted.yaml")]
    [InlineData("baseline-30.json")]
    [InlineData("baseline-31.yaml")]
    [InlineData("candidate-additive.yaml")]
    [InlineData("candidate-breaking.yaml")]
    [InlineData("candidate-conflict.yaml")]
    [InlineData("candidate-doc-only.yaml")]
    [InlineData("cyclic.yaml")]
    [InlineData("redundant.yaml")]
    [InlineData("secrets.yaml")]
    public async Task An_unmodified_tree_rewrites_itself_byte_for_byte(string fileName)
    {
        // L'invariant sur lequel repose toute la fidélité de l'outil : l'arbre couvre le document. Un nœud
        // oublié ou une étendue mal calculée se voit ici comme une différence d'octets.
        SpecReadResult result = await ReadAsync(fileName);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        ReadOnlyMemory<byte> rewritten = new VerbatimEmitter().Emit(result.Document!);

        rewritten.ToArray().Should().Equal(result.Document!.Source.ToArray());
    }


    [Theory]
    [InlineData("baseline-30.yaml")]
    [InlineData("baseline-30.json")]
    [InlineData("baseline-31.yaml")]
    [InlineData("candidate-breaking.yaml")]
    [InlineData("secrets.yaml")]
    public async Task Every_scalar_span_designates_that_scalar_in_the_bytes(string fileName)
    {
        // L'épreuve de va-et-vient ne peut pas établir ceci, et c'est pourquoi celle-ci existe. Elle
        // concatène les étendues dans l'ordre du document, si bien que les intervalles entre elles
        // absorbent n'importe quel décalage : un arbre dont chaque étendue serait décalée de quatre
        // octets se réécrirait quand même à l'identique. Prise isolément, une étendue doit désigner son
        // nœud — c'est ce dont dépendent la mesure des tailles et toute réécriture partielle.
        SpecReadResult result = await ReadAsync(fileName);

        result.Document.Should().NotBeNull();

        List<string> wrong = [];

        Visit(result.Document.Root, node =>
        {
            if (node is not ScalarNode scalar || scalar.Style is not ScalarStyle.Plain
                || scalar.Text.Length == 0)
            {
                return;
            }

            ReadOnlySpan<byte> source = result.Document.Source.Span;

            if (scalar.Span.End > source.Length)
            {
                wrong.Add(scalar.Location.Display + " : étendue hors du document");

                return;
            }

            string written = Encoding.UTF8.GetString(
                source[(int)scalar.Span.Start..(int)scalar.Span.End]);

            if (!written.StartsWith(scalar.Text, StringComparison.Ordinal))
            {
                wrong.Add(
                    scalar.Location.Display + " : les octets disent « " + written
                    + " » et le nœud dit « " + scalar.Text + " »");
            }
        });

        wrong.Should().BeEmpty(
            "une étendue doit désigner son nœud : " + string.Join(" · ", wrong.Take(5)));
    }

    /// <summary>Parcourt l'arbre entier.</summary>
    private static void Visit(SpecNode node, Action<SpecNode> visitor)
    {
        visitor(node);

        switch (node)
        {
            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    Visit(entry.Key, visitor);
                    Visit(entry.Value, visitor);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    Visit(item, visitor);
                }

                break;

            default:
                break;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData("baseline-30.yaml")]
    [InlineData("baseline-30.json")]
    public async Task A_file_carrying_a_byte_order_mark_is_still_openapi(string fileName)
    {
        // Le défaut tel qu'il a été rencontré : Visual Studio, le Bloc-notes et la plupart des outils
        // Windows écrivent « EF BB BF » en tête. La première clé était lue comme « ﻿openapi », qui
        // n'est pas « openapi », et l'outil refusait le fichier en annonçant qu'il ne déclarait pas sa
        // version — ce qui envoyait son auteur chercher une faute dans un fichier juste. En JSON le
        // symptôme différait : « Utf8JsonReader » refusait l'octet, donc l'analyse échouait.
        SpecReadResult read = await ReadAsync(WithMark(fileName), fromCorpus: false);

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(one => one.Code + " " + one.Message)));

        read.Diagnostics.Should().NotContain(
            one => one.Code == DiagnosticCodes.MissingVersion || one.Code == DiagnosticCodes.ParseFailed);
    }

    [Theory]
    [InlineData("baseline-30.yaml")]
    [InlineData("baseline-30.json")]
    public async Task A_file_carrying_a_byte_order_mark_rewrites_itself_byte_for_byte(string fileName)
    {
        // La nomenclature fait partie du fichier, et un document consolidé la conserve : la longueur est
        // comptée, les octets ne sont pas retirés. Sans quoi l'outil ferait perdre trois octets que
        // personne n'a demandé de perdre — et cette épreuve verrait la différence.
        SpecReadResult read = await ReadAsync(WithMark(fileName), fromCorpus: false);

        read.IsFaithful.Should().BeTrue();

        ReadOnlyMemory<byte> rewritten = new VerbatimEmitter().Emit(read.Document!);

        rewritten.ToArray().Should().Equal(
            read.Document!.Source.ToArray(), "l'arbre doit couvrir le fichier, nomenclature comprise");

        read.Document.Source.Span[..3].ToArray().Should().Equal(
            [0xEF, 0xBB, 0xBF], "la nomenclature doit toujours être là");
    }

    [Theory]
    [InlineData("baseline-30.yaml")]
    [InlineData("baseline-30.json")]
    public async Task Every_scalar_span_of_a_marked_file_designates_that_scalar(string fileName)
    {
        // L'épreuve qui tient la base des étendues. Une base oubliée décale tout de trois octets, ce
        // qu'aucun document sans nomenclature ne révèle — et l'épissure écrirait alors à côté.
        SpecReadResult read = await ReadAsync(WithMark(fileName), fromCorpus: false);

        read.IsFaithful.Should().BeTrue();

        ReadOnlySpan<byte> bytes = read.Document!.Source.Span;

        foreach (ScalarNode scalar in Scalars(read.Document.Root).Take(60))
        {
            string designated = Encoding.UTF8.GetString(
                bytes.Slice((int)scalar.Span.Start, (int)scalar.Span.Length));

            designated.Should().Contain(
                scalar.Text.Length > 12 ? scalar.Text[..12] : scalar.Text,
                scalar.Location.Display + " : l'étendue doit désigner ce scalaire");
        }
    }

    /// <summary>Écrit un fichier du corpus précédé d'une nomenclature d'octets.</summary>
    private string WithMark(string fileName)
    {
        string path = Path.Combine(_root, "nomenclature-" + fileName);

        File.WriteAllBytes(
            path,
            [.. (byte[])[0xEF, 0xBB, 0xBF], .. Encoding.UTF8.GetBytes(CorpusLoader.Read(fileName))]);

        return path;
    }

    /// <summary>Les scalaires de l'arbre, dans l'ordre du document.</summary>
    private static IEnumerable<ScalarNode> Scalars(SpecNode node)
    {
        switch (node)
        {
            case ScalarNode scalar:
                yield return scalar;

                break;

            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    yield return entry.Key;

                    foreach (ScalarNode nested in Scalars(entry.Value))
                    {
                        yield return nested;
                    }
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    foreach (ScalarNode nested in Scalars(item))
                    {
                        yield return nested;
                    }
                }

                break;

            default:
                break;
        }
    }

    private string Write(string fileName, string content)
    {
        string path = Path.Combine(_root, fileName);
        File.WriteAllText(path, content);

        return path;
    }

    private Task<SpecReadResult> ReadAsync(
        string pathOrName,
        bool fromCorpus = true,
        LoadLimits? limits = null)
    {
        string path = fromCorpus
            ? Write(pathOrName, CorpusLoader.Read(pathOrName))
            : pathOrName;

        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        return reader.ReadAsync(
            path, DocumentRole.Production, limits ?? LoadLimits.Default, CancellationToken.None);
    }
}
