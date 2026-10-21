using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Dependencies;

/// <summary>
/// Ce que la fermeture doit emporter pour que ce qui est reporté tienne debout.
/// </summary>
/// <remarks>
/// Ce fichier s'appelait <c>PatchTests</c> et éprouvait un fragment, sortie que la clarification du
/// 2026-10-21 a retirée. Ce qui restait vrai une fois le fragment parti est ici : la fermeture
/// transitive, les désaccords d'homonymes et les cycles légaux ne dépendaient pas de la forme de la
/// sortie. Ce qui en dépendait a été retiré avec elle, et notamment l'épreuve qui exigeait qu'une
/// opération modifiée soit emportée <em>entière</em> — une épissure remplace exactement l'emplacement
/// visé, et FR-503 demande maintenant le contraire.
/// </remarks>
public sealed class ClosureTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-fermeture-" + Guid.NewGuid().ToString("N"));

    public ClosureTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_new_operation_brings_the_whole_chain_of_schemas_it_references()
    {
        // La nouvelle opération référence DossierDetail, qui référence Dossier et Document, qui référence
        // Dossier de nouveau — un cycle légal qui ne doit ni manquer ni boucler.
        DependencyClosure closure = await CloseAsync("candidate-additive.yaml");

        IEnumerable<string> names = closure.Required.Select(pointer => pointer.Json);

        names.Should().Contain("/components/schemas/DossierDetail");
        names.Should().Contain("/components/schemas/Dossier");
        names.Should().Contain("/components/schemas/Document");
    }

    [Fact]
    public async Task Path_level_declarations_travel_with_the_operations_they_govern()
    {
        // Un paramètre déclaré au niveau du chemin vaut pour chaque opération de ce chemin : le laisser
        // derrière changerait ce que l'opération exige.
        DependencyClosure closure = await CloseAsync("candidate-breaking.yaml");

        closure.Required.Select(pointer => pointer.Json)
            .Should().Contain("/components/parameters/XServiceEmetteur");
    }

    [Fact]
    public async Task A_component_that_keeps_its_name_while_changing_is_reported_as_a_conflict()
    {
        DependencyClosure closure = await CloseAsync("candidate-conflict.yaml");

        ComponentConflict conflict = closure.Conflicts.Should().ContainSingle().Subject;

        conflict.Name.Should().Be("Dossier");
        conflict.Kind.Should().Be("schemas");

        // Nommer les champs est ce qui rend le désaccord tranchable.
        conflict.DifferingFields.Should().Contain("required");
        conflict.DifferingFields.Should().Contain("properties");
    }

    [Fact]
    public async Task An_identical_component_is_not_a_conflict()
    {
        DependencyClosure closure = await CloseAsync("candidate-additive.yaml");

        closure.Conflicts.Should().BeEmpty(
            "deux écritures du même composant ne sont pas un désaccord");
    }

    [Fact]
    public async Task A_legal_cycle_terminates_and_is_recorded()
    {
        DependencyClosure closure = await CloseAsync("candidate-additive.yaml");

        closure.Cycles.Should().NotBeEmpty("Dossier se référence lui-même par son parent");
        closure.Required.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<DependencyClosure> CloseAsync(string candidateName)
    {
        SpecDocument candidate = await ReadAsync(candidateName);
        SpecDocument production = await ReadAsync("baseline-30.yaml");

        IReadOnlyList<Difference> differences = new DocumentComparer(new OpenApiMetamodel())
            .Compare(candidate, production, CancellationToken.None)
            .Value;

        return new DependencyResolver(new OpenApiMetamodel())
            .Close(
                [.. differences.Select(difference => difference.Location)],
                candidate,
                production,
                CancellationToken.None)
            .Value;
    }

    private async Task<SpecDocument> ReadAsync(string fileName)
    {
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + "-" + fileName);
        await File.WriteAllTextAsync(path, CorpusLoader.Read(fileName));

        SpecReadResult result = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return result.Document!;
    }
}
