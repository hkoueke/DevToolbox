using DevToolbox.Domain.Results;
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
/// La raison d'être de l'outil : une description réécrite autrement n'a pas changé.
/// </summary>
/// <remarks>
/// Si l'un de ces essais échoue, tout le reste est suspect : un rapport qui signale la remise en forme
/// noie les trois lignes qui comptent sous quatre cents qui ne comptent pas, et c'est exactement ce qu'un
/// diff de texte fait déjà.
/// </remarks>
public sealed class FormattingIsNotDifferenceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-mise-en-forme-" + Guid.NewGuid().ToString("N"));

    public FormattingIsNotDifferenceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_description_compared_to_itself_has_not_changed()
    {
        IReadOnlyList<Difference> differences = await CompareAsync(
            "baseline-30.yaml", "baseline-30.yaml");

        differences.Should().BeEmpty();
    }

    [Fact]
    public async Task Reindentation_reordering_and_requoting_are_not_changes()
    {
        // L'autre fichier porte quatre espaces d'indentation, un autre ordre de clés de premier niveau,
        // des codes de réponse non quotés, des ensembles « required » réordonnés et des commentaires.
        IReadOnlyList<Difference> differences = await CompareAsync(
            "baseline-30.reformatted.yaml", "baseline-30.yaml");

        differences.Should().BeEmpty(
            string.Join(
                " · ",
                differences.Select(difference =>
                    difference.Location.Json + " " + difference.ChangeId)));
    }

    [Fact]
    public async Task The_same_description_in_json_and_in_yaml_is_the_same_description()
    {
        IReadOnlyList<Difference> differences = await CompareAsync(
            "baseline-30.json", "baseline-30.yaml");

        differences.Should().BeEmpty(
            string.Join(
                " · ",
                differences.Select(difference =>
                    difference.Location.Json + " " + difference.ChangeId)));
    }

    [Fact]
    public async Task Changed_line_endings_are_not_changes()
    {
        string windows = Write("crlf.yaml", CorpusLoader.ReadWithCrLf("baseline-30.yaml"));
        string unix = Write("lf.yaml", CorpusLoader.Read("baseline-30.yaml"));

        IReadOnlyList<Difference> differences = await CompareFilesAsync(windows, unix);

        differences.Should().BeEmpty();
    }

    [Fact]
    public async Task A_corrected_description_is_documentation_only()
    {
        IReadOnlyList<Difference> differences = await CompareAsync(
            "candidate-doc-only.yaml", "baseline-30.yaml");

        differences.Should().NotBeEmpty();
        differences.Should().OnlyContain(
            difference => difference.Impact == ChangeImpact.DocumentationOnly);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Write(string fileName, string content)
    {
        string path = Path.Combine(_root, fileName);
        File.WriteAllText(path, content);

        return path;
    }

    private Task<IReadOnlyList<Difference>> CompareAsync(string candidateName, string productionName) =>
        CompareFilesAsync(
            Write(candidateName, CorpusLoader.Read(candidateName)),
            Write(productionName, CorpusLoader.Read(productionName)));

    private static async Task<IReadOnlyList<Difference>> CompareFilesAsync(
        string candidatePath,
        string productionPath)
    {
        SpecReader reader = new(NullLogger<SpecReader>.Instance);

        SpecReadResult candidate = await reader.ReadAsync(
            candidatePath, DocumentRole.Candidate, LoadLimits.Default, CancellationToken.None);

        SpecReadResult production = await reader.ReadAsync(
            productionPath, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        candidate.IsFaithful.Should().BeTrue();
        production.IsFaithful.Should().BeTrue();

        Result<IReadOnlyList<Difference>> result = new DocumentComparer(new OpenApiMetamodel())
            .Compare(candidate.Document!, production.Document!, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        return result.Value;
    }
}
