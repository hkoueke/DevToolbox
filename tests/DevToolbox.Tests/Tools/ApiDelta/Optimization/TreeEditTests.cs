using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Optimization;

/// <summary>
/// Ce que les trois éditions font à l'arbre, et ce qu'elles laissent intact.
/// </summary>
public sealed class TreeEditTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-editions-" + Guid.NewGuid().ToString("N"));

    public TreeEditTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task An_edit_returns_a_new_tree_and_leaves_the_original_intact()
    {
        // C'est ce qui rend l'optimiseur vérifiable : l'arbre d'origine reste disponible pour la
        // comparaison d'équivalence, au moment précis où l'on en a besoin.
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Simple);

        SpecNode edited = TreeEdits.Remove(
            document.Root, SpecPointer.Root.Child("paths").Child("/un").Child("get"));

        PointerNavigator.Find(edited, "/paths/~1un/get").Should().BeNull();
        PointerNavigator.Find(document.Root, "/paths/~1un/get").Should().NotBeNull();
    }

    [Fact]
    public async Task Replacing_a_node_keeps_its_siblings_and_its_place()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Simple);

        SpecNode edited = TreeEdits.Replace(
            document.Root,
            SpecPointer.Root.Child("paths").Child("/un").Child("get").Child("parameters").Item(0),
            TreeEdits.Reference("#/components/parameters/X"));

        PointerNavigator.Find(edited, "/paths/~1un/get/parameters/0")
            .Should().BeOfType<MappingNode>()
            .Which.Find("$ref").Should().BeOfType<ScalarNode>()
            .Which.Text.Should().Be("#/components/parameters/X");

        PointerNavigator.Find(edited, "/paths/~1un/get/parameters/1")
            .Should().NotBeNull("le voisin reste à son rang");

        PointerNavigator.Find(edited, "/paths/~1un/get/operationId")
            .Should().NotBeNull("les champs frères ne bougent pas");
    }

    [Fact]
    public async Task Inserting_creates_the_missing_mappings_of_the_path()
    {
        // Sans cela, la première extraction serait impossible : le document n'a pas de « components ».
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Simple);

        PointerNavigator.Find(document.Root, "/components").Should().BeNull();

        SpecNode edited = TreeEdits.Insert(
            document.Root,
            SpecPointer.Root.Child("components").Child("parameters").Child("X"),
            TreeEdits.Mapping(
                SpecPointer.Root, [new MappingEntry(TreeEdits.Key("name"), TreeEdits.Key("X-un"))]));

        PointerNavigator.Find(edited, "/components/parameters/X")
            .Should().BeOfType<MappingNode>()
            .Which.Find("name").Should().BeOfType<ScalarNode>().Which.Text.Should().Be("X-un");
    }

    [Fact]
    public async Task An_inserted_subtree_carries_its_new_location_and_not_the_one_it_came_from()
    {
        // Le métamodèle lit l'emplacement pour savoir ce qu'un champ est. Un sous-arbre déplacé qui
        // garderait celui de sa provenance l'y tromperait.
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Simple);

        SpecNode parameter = PointerNavigator.Find(
            document.Root, "/paths/~1un/get/parameters/0")!;

        parameter.Location.Json.Should().Be("/paths/~1un/get/parameters/0");

        SpecNode edited = TreeEdits.Insert(
            document.Root,
            SpecPointer.Root.Child("components").Child("parameters").Child("X"),
            parameter);

        PointerNavigator.Find(edited, "/components/parameters/X")!.Location.Json
            .Should().Be("/components/parameters/X");

        PointerNavigator.Find(edited, "/components/parameters/X/schema")!.Location.Json
            .Should().Be("/components/parameters/X/schema", "jusqu'au fond du sous-arbre");
    }

    [Fact]
    public async Task Removing_an_item_renumbers_the_ones_that_follow()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Simple);

        SpecNode edited = TreeEdits.Remove(
            document.Root,
            SpecPointer.Root.Child("paths").Child("/un").Child("get").Child("parameters").Item(0));

        SequenceNode parameters = PointerNavigator
            .Find(edited, "/paths/~1un/get/parameters")
            .Should().BeOfType<SequenceNode>().Subject;

        parameters.Items.Should().ContainSingle();

        parameters.Items[0].Location.Json
            .Should().Be("/paths/~1un/get/parameters/0", "l'ancien second est devenu le premier");

        (parameters.Items[0] as MappingNode)!.Find("name")
            .Should().BeOfType<ScalarNode>().Which.Text.Should().Be("X-deux");
    }

    [Fact]
    public async Task An_edit_that_designates_nothing_changes_nothing()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Simple);

        TreeEdits.Remove(document.Root, SpecPointer.Root.Child("absent"))
            .Should().BeSameAs(document.Root);

        TreeEdits.Remove(document.Root, SpecPointer.Root)
            .Should().BeSameAs(document.Root, "retirer la racine n'est pas une édition");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private const string Simple = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            get:
              operationId: lireUn
              parameters:
                - name: X-un
                  in: header
                  schema:
                    type: string
                - name: X-deux
                  in: header
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
        """;
}
