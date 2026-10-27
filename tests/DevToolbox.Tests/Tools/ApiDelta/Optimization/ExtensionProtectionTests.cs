using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Optimization;

/// <summary>
/// OPT-010 : ce qu'aucune règle n'a le droit de faire disparaître.
/// </summary>
public sealed class ExtensionProtectionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-extensions-" + Guid.NewGuid().ToString("N"));

    public ExtensionProtectionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Every_rule_run_against_the_reference_corpus_leaves_every_extension_in_place()
    {
        // Toutes les règles, sur le document du corpus qui porte des extensions de fournisseur. Ce que
        // cet essai établit n'est pas qu'une règle est correcte, mais qu'aucune ne perd en chemin une
        // donnée dont l'outil ne sait rien.
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "baseline-30.yaml");

        IReadOnlyList<string> before = Named(document.Root);

        before.Should().NotBeEmpty("le document de référence doit porter des extensions");

        foreach (IOptimizationRule rule in OptimizationHarness.Rules)
        {
            SpecNode edited = Everything(document, rule);

            ExtensionProtection.Survived(document.Root, edited)
                .Should().BeEmpty("la règle {0} ne doit perdre aucune extension", rule.RuleId);
        }
    }

    [Fact]
    public async Task A_lost_extension_is_named_rather_than_counted()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "baseline-30.yaml");

        SpecPointer? carrier = Carrier(document.Root);

        carrier.Should().NotBeNull("le document doit porter une extension à retirer");

        SpecNode mutilated = TreeEdits.Remove(document.Root, carrier);

        ExtensionProtection.Survived(document.Root, mutilated)
            .Should().ContainSingle()
            .Which.Should().Be(carrier.Segments[^1]);
    }

    [Fact]
    public async Task An_extension_moved_without_being_altered_counts_as_surviving()
    {
        // Une extraction déplace l'objet qui porte l'extension : la donnée est toujours là, une fois au
        // lieu de quatorze, et l'équivalence de contrat prouve séparément que le document dit la même
        // chose. C'est la perte qui est interdite, pas le déplacement d'une extension quelconque.
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Ordinary);
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        OptimizationOutcome outcome = OptimizationHarness.Applier().Apply(
            plan,
            document,
            new RunSelection(
                RunMode.Analyse, "p.yaml", string.Empty, "s", ApplySafeOptimizations: true),
            Ruleset.Default);

        outcome.Applied.Should().NotBeEmpty();
        outcome.Verification.Established.Should().BeTrue(outcome.Verification.Explanation);

        System.Text.Encoding.UTF8.GetString(outcome.Serialized.Span)
            .Should().Contain("x-quelconque");
    }

    [Fact]
    public async Task An_object_under_a_protected_prefix_is_not_a_candidate_at_all()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Protected);

        SpecNode parameter = PointerNavigator.Find(
            document.Root, "/paths/~1un/get/parameters/0")!;

        ExtensionProtection.Guards(parameter, Ruleset.Default).Should().BeFalse(
            "l'outil ne sait pas ce qu'une passerelle lit : il n'y touche pas");

        // Le même objet sans le préfixe protégé est libre d'être transformé.
        SpecDocument ordinary = await OptimizationHarness.WrittenAsync(_root, Ordinary);

        ExtensionProtection.Guards(
                PointerNavigator.Find(ordinary.Root, "/paths/~1un/get/parameters/0")!,
                Ruleset.Default)
            .Should().BeTrue();
    }

    [Fact]
    public async Task A_ruleset_can_only_add_protection_never_remove_it()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Ordinary);

        SpecNode parameter = PointerNavigator.Find(document.Root, "/paths/~1un/get/parameters/0")!;

        Ruleset wider = Ruleset.Default with { ProtectedExtensionPrefixes = ["x-sg-", "x-quelcon"] };

        ExtensionProtection.Guards(parameter, wider).Should().BeFalse(
            "un préfixe déclaré protégé le devient");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Fait tourner une règle sur tout ce que le document lui offre, sans égard pour la pertinence.
    /// </summary>
    /// <remarks>
    /// Sans égard : l'invariant d'OPT-010 ne dépend pas de ce qu'une transformation est censée faire. Une
    /// règle appliquée à un emplacement qui ne lui convient pas doit échouer ou ne rien faire — jamais
    /// perdre une extension au passage.
    /// </remarks>
    private static SpecNode Everything(SpecDocument document, IOptimizationRule rule)
    {
        SpecNode edited = document.Root;

        foreach (SpecPointer origin in Carriers(document.Root).Take(20))
        {
            Transformation transformation = new(
                rule.RuleId,
                SafetyClass.Safe,
                [origin],
                SpecPointer.Root.Child("components").Child("schemas").Child("Essai"),
                Evidence: "abc",
                new ReversalDescription("défaire", Reversible: true),
                TransformationState.Proposed);

            if (!ExtensionProtection.Guards(
                    PointerNavigator.Find(edited, origin.Json) ?? edited,
                    Ruleset.Default,
                    rule.Removes))
            {
                // L'applicateur est la seule porte : il écarte cet objet avant qu'une règle le voie.
                continue;
            }

            if (rule.Apply(edited, transformation) is { IsSuccess: true } applied)
            {
                edited = applied.Value;
            }
        }

        return edited;
    }

    /// <summary>Les objets qui portent une extension, et qu'une règle pourrait donc abîmer.</summary>
    private static IEnumerable<SpecPointer> Carriers(SpecNode node)
    {
        Stack<SpecNode> pending = new();
        pending.Push(node);

        while (pending.Count > 0)
        {
            SpecNode current = pending.Pop();

            if (current is SequenceNode sequence)
            {
                foreach (SpecNode item in sequence.Items)
                {
                    pending.Push(item);
                }

                continue;
            }

            if (current is not MappingNode mapping)
            {
                continue;
            }

            if (mapping.Entries.Any(entry =>
                    entry.Key.Text.StartsWith("x-", StringComparison.Ordinal)))
            {
                yield return current.Location;
            }

            foreach (MappingEntry entry in mapping.Entries)
            {
                pending.Push(entry.Value);
            }
        }
    }

    /// <summary>L'emplacement d'une extension, pour pouvoir la retirer.</summary>
    private static SpecPointer? Carrier(SpecNode node) =>
        Carriers(node)
            .Select(carrier => PointerNavigator.Find(node, carrier.Json) as MappingNode)
            .OfType<MappingNode>()
            .SelectMany(mapping => mapping.Entries
                .Where(entry => entry.Key.Text.StartsWith("x-", StringComparison.Ordinal))
                .Select(entry => mapping.Location.Child(entry.Key.Text)))
            .FirstOrDefault();

    private static IReadOnlyList<string> Named(SpecNode node) =>
        [.. Carriers(node).Select(carrier => carrier.Json)];

    private const string Ordinary = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            get:
              operationId: lireUn
              parameters:
                - name: X-serviceEmetteur
                  in: header
                  required: true
                  description: Le service appelant, nomme par convention interne.
                  x-quelconque:
                    note: sans portee connue
                  schema:
                    type: string
                    maxLength: 32
              responses:
                '200':
                  description: Bon.
          /deux:
            get:
              operationId: lireDeux
              parameters:
                - name: X-serviceEmetteur
                  in: header
                  required: true
                  description: Le service appelant, nomme par convention interne.
                  x-quelconque:
                    note: sans portee connue
                  schema:
                    type: string
                    maxLength: 32
              responses:
                '200':
                  description: Bon.
        """;

    private const string Protected = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            get:
              operationId: lireUn
              parameters:
                - name: X-serviceEmetteur
                  in: header
                  required: true
                  x-sg-passerelle:
                    catalogue: essai
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
        """;
}
