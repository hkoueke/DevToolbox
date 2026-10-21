using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Comparison;

/// <summary>
/// Un changement, une différence, au bon endroit.
/// </summary>
/// <remarks>
/// Sans cette règle, un mot corrigé dans une description produirait un « modifié » sur la description, sur
/// l'opération, sur l'élément de chemin, sur <c>paths</c> et sur le document : cinq lignes pour un mot, et
/// un rapport d'autant moins lisible qu'il y a de choses à lire.
/// </remarks>
public sealed class AttributionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-attribution-" + Guid.NewGuid().ToString("N"));

    public AttributionTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("candidate-breaking.yaml")]
    [InlineData("candidate-additive.yaml")]
    [InlineData("candidate-doc-only.yaml")]
    [InlineData("candidate-conflict.yaml")]
    public async Task No_difference_is_reported_inside_another(string candidateName)
    {
        IReadOnlyList<Difference> differences = await CompareAsync(candidateName);

        IReadOnlyList<string> locations = [.. differences.Select(difference => difference.Location.Json)];

        foreach (string outer in locations)
        {
            IReadOnlyList<string> inside =
            [
                .. locations.Where(inner =>
                    !string.Equals(inner, outer, StringComparison.Ordinal)
                    && inner.StartsWith(outer + "/", StringComparison.Ordinal)),
            ];

            inside.Should().BeEmpty(
                "{0} contiendrait déjà {1}", outer, string.Join(", ", inside));
        }
    }

    [Fact]
    public async Task A_corrected_description_is_reported_on_the_description_alone()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-doc-only.yaml");

        // Le candidat ne corrige que trois textes : la description du document, le nom du contact et le
        // résumé d'une opération.
        differences.Should().HaveCount(3);

        differences.Select(difference => difference.Location.Json).Should().BeEquivalentTo(
            "/info/contact/name",
            "/info/description",
            "/paths/~1documents~1{id}/get/summary");
    }

    [Fact]
    public async Task An_added_path_is_reported_once_and_not_once_per_node_beneath_it()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-additive.yaml");

        IReadOnlyList<Difference> underNewPath =
        [
            .. differences.Where(difference =>
                difference.Location.Json.Contains("~1dossiers~1{id}", StringComparison.Ordinal)),
        ];

        underNewPath.Should().ContainSingle(
            "un chemin apparu est une différence, non une par nœud de son sous-arbre");

        underNewPath[0].Kind.Should().Be(DifferenceKind.Added);
        underNewPath[0].After.Should().NotBeNull();
    }

    [Fact]
    public async Task A_difference_names_its_place_in_words_a_developer_can_read()
    {
        IReadOnlyList<Difference> differences = await CompareAsync("candidate-breaking.yaml");

        Difference response = differences.Single(difference =>
            string.Equals(
                difference.Location.Json,
                "/paths/~1documents~1{id}/get/responses/404",
                StringComparison.Ordinal));

        // La forme lisible dit le chemin parcouru, pas seulement le pointeur.
        response.Location.Display.Should().Contain("paths").And.Contain("404");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<IReadOnlyList<Difference>> CompareAsync(string candidateName)
    {
        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        SpecReadResult candidate = await reader.ReadAsync(
            Write(candidateName), DocumentRole.Candidate, LoadLimits.Default, CancellationToken.None);

        SpecReadResult production = await reader.ReadAsync(
            Write("baseline-30.yaml"), DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        return new DocumentComparer(new OpenApiMetamodel())
            .Compare(candidate.Document!, production.Document!, CancellationToken.None)
            .Value;
    }

    private string Write(string fileName)
    {
        string path = Path.Combine(_root, fileName);
        File.WriteAllText(path, CorpusLoader.Read(fileName));

        return path;
    }
}
