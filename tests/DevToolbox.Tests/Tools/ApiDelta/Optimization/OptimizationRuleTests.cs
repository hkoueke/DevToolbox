using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Optimization;

/// <summary>
/// Ce que chaque règle d'optimisation fait à l'arbre, et ce qu'elle refuse de faire.
/// </summary>
public sealed class OptimizationRuleTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-regles-opt-" + Guid.NewGuid().ToString("N"));

    public OptimizationRuleTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task OPT_002_extracts_the_parameter_and_leaves_a_reference_at_each_origin()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        Transformation transformation = OptimizationHarness.Of(plan, "OPT-002");

        SpecNode edited = OptimizationHarness.Rule("OPT-002")
            .Apply(document.Root, transformation).Value;

        PointerNavigator.Find(edited, "/components/parameters/XServiceEmetteur")
            .Should().BeOfType<MappingNode>();

        foreach (SpecPointer origin in transformation.Origins)
        {
            MappingNode replaced = PointerNavigator.Find(edited, origin.Json)
                .Should().BeOfType<MappingNode>().Subject;

            replaced.Find("$ref").Should().BeOfType<ScalarNode>()
                .Which.Text.Should().Be("#/components/parameters/XServiceEmetteur");
        }
    }

    [Fact]
    public async Task OPT_002_leaves_override_behaviour_alone_because_it_replaces_in_place()
    {
        // L'extraction ne déplace rien : un paramètre déclaré au niveau de l'opération continue de
        // masquer celui du chemin, puisqu'il est toujours déclaré au niveau de l'opération.
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        SpecNode edited = OptimizationHarness.Rule("OPT-002")
            .Apply(document.Root, OptimizationHarness.Of(plan, "OPT-002")).Value;

        PointerNavigator.Find(edited, "/paths/~1ressource1/get/parameters/0")
            .Should().NotBeNull("l'occurrence reste là où elle était, sous forme de référence");
    }

    [Fact]
    public async Task OPT_003_and_OPT_004_extract_into_the_family_their_location_designates()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        SpecNode edited = OptimizationHarness.Rule("OPT-003")
            .Apply(document.Root, OptimizationHarness.Of(plan, "OPT-003")).Value;

        edited = OptimizationHarness.Rule("OPT-004")
            .Apply(edited, OptimizationHarness.Of(plan, "OPT-004")).Value;

        PointerNavigator.Find(edited, "/components/responses/Reponse400").Should().NotBeNull();
        PointerNavigator.Find(edited, "/components/requestBodies/PostRequestBody")
            .Should().NotBeNull();
    }

    [Fact]
    public async Task OPT_005_extracts_a_repeated_response_header_into_the_headers_family()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Headers);
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        Transformation transformation = OptimizationHarness.Of(plan, "OPT-005");

        transformation.Destination!.Json.Should().StartWith("/components/headers/");

        SpecNode edited = OptimizationHarness.Rule("OPT-005")
            .Apply(document.Root, transformation).Value;

        PointerNavigator.Find(edited, transformation.Destination.Json).Should().NotBeNull();
    }

    [Fact]
    public void OPT_006_does_not_run_by_default()
    {
        // Extraire un schéma écrit sur place lui donne un nom, et ce nom devient un type public chez
        // chaque appelant. L'outil ne le fait pas sans qu'on le demande.
        OptimizationHarness.Rule("OPT-006").EnabledByDefault.Should().BeFalse();
        OptimizationDefaults.EnabledByDefault("OPT-006").Should().BeFalse();
        OptimizationDefaults.Enabled("OPT-006", Ruleset.Default).Should().BeFalse();
    }

    [Fact]
    public void OPT_009_does_not_run_by_default_either()
    {
        OptimizationHarness.Rule("OPT-009").EnabledByDefault.Should().BeFalse();
        OptimizationDefaults.Enabled("OPT-009", Ruleset.Default).Should().BeFalse();
    }

    [Fact]
    public void A_ruleset_can_turn_a_default_off_rule_on_and_a_default_on_rule_off()
    {
        Ruleset ruleset = Ruleset.Default with
        {
            Rules = new Dictionary<string, RuleSetting>(StringComparer.Ordinal)
            {
                ["OPT-006"] = RuleSetting.Default with { Enabled = true },
                ["OPT-002"] = RuleSetting.Default with { Enabled = false },
            },
        };

        OptimizationDefaults.Enabled("OPT-006", ruleset).Should().BeTrue();
        OptimizationDefaults.Enabled("OPT-002", ruleset).Should().BeFalse();
    }

    [Fact]
    public async Task OPT_007_removes_the_component_the_plan_names_and_nothing_more()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        Transformation transformation = OptimizationHarness.Of(plan, "OPT-007");
        string removed = transformation.Origins[0].Json;

        SpecNode edited = OptimizationHarness.Rule("OPT-007")
            .Apply(document.Root, transformation).Value;

        PointerNavigator.Find(edited, removed).Should().BeNull();
        PointerNavigator.Find(edited, "/components/schemas/Ressource")
            .Should().NotBeNull("un composant atteint n'est pas touché");
    }

    [Fact]
    public async Task OPT_007_refuses_to_remove_what_is_no_longer_there()
    {
        // Retirer deux fois le même emplacement retirerait quelque chose d'autre : les clés voisines ont
        // changé de place. La règle refuse plutôt que d'éditer à l'aveugle.
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        Transformation transformation = OptimizationHarness.Of(plan, "OPT-007");

        SpecNode once = OptimizationHarness.Rule("OPT-007")
            .Apply(document.Root, transformation).Value;

        Result<SpecNode> twice = OptimizationHarness.Rule("OPT-007").Apply(once, transformation);

        twice.IsFailure.Should().BeTrue();
        twice.Failure!.Message.Should().Contain("n'est plus là");
    }

    [Fact]
    public async Task OPT_008_hoists_only_where_every_operation_of_the_path_carries_the_headers()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, TwoHeaders);
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        Transformation transformation = OptimizationHarness.Of(plan, "OPT-008");

        SpecNode edited = OptimizationHarness.Rule("OPT-008")
            .Apply(document.Root, transformation).Value;

        // Le chemin complet est remonté ; celui dont une opération ne porte pas les en-têtes est intact.
        PointerNavigator.Find(edited, "/paths/~1complet/parameters")
            .Should().BeOfType<SequenceNode>().Which.Items.Should().HaveCount(2);

        PointerNavigator.Find(edited, "/paths/~1complet/get/parameters")
            .Should().BeNull("la clé vidée est retirée, et non laissée vide");

        PointerNavigator.Find(edited, "/paths/~1partiel/parameters")
            .Should().BeNull("remonter ici ajouterait les en-têtes à l'opération qui ne les portait pas");

        PointerNavigator.Find(edited, "/paths/~1partiel/get/parameters")
            .Should().NotBeNull();
    }

    [Fact]
    public async Task OPT_008_fails_rather_than_pass_for_done_when_no_path_qualifies()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, OnlyPartial);
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        Result<SpecNode> applied = OptimizationHarness.Rule("OPT-008")
            .Apply(document.Root, OptimizationHarness.Of(plan, "OPT-008"));

        applied.IsFailure.Should().BeTrue();
        applied.Failure!.Message.Should().Contain("toutes ses opérations");
    }

    [Fact]
    public async Task OPT_009_poses_the_flag_where_the_description_announces_the_deprecation()
    {
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Deprecated);

        Ruleset ruleset = Ruleset.Default with
        {
            Rules = new Dictionary<string, RuleSetting>(StringComparer.Ordinal)
            {
                ["OPT-009"] = RuleSetting.Default with { Enabled = true },
            },
        };

        OptimizationPlan plan = OptimizationHarness.Plan(document, ruleset);

        SpecNode edited = OptimizationHarness.Rule("OPT-009")
            .Apply(document.Root, OptimizationHarness.Of(plan, "OPT-009")).Value;

        PointerNavigator.Find(edited, "/paths/~1vieux/get/deprecated")
            .Should().BeOfType<ScalarNode>().Which.Resolved.Canonical.Should().Be("true");
    }

    [Fact]
    public async Task OPT_009_does_not_contradict_an_explicit_decision()
    {
        // Un « deprecated: false » explicite est une décision. L'écraser sans le dire serait pire que de
        // ne rien faire.
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, DeprecatedFalse);

        Transformation transformation = new(
            "OPT-009",
            SafetyClass.ReviewRequired,
            [SpecPointer.Root.Child("paths").Child("/vieux").Child("get")],
            Destination: null,
            Evidence: null,
            new ReversalDescription("retirer le drapeau", Reversible: true),
            TransformationState.Proposed);

        Result<SpecNode> applied = OptimizationHarness.Rule("OPT-009")
            .Apply(document.Root, transformation);

        applied.IsFailure.Should().BeTrue();
        applied.Failure!.Message.Should().Contain("déjà posé");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private const string Headers = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            get:
              operationId: lireUn
              responses:
                '200':
                  description: Bon.
                  headers:
                    X-suivi:
                      description: Identifiant de suivi de la demande, toujours renseigne.
                      schema:
                        type: string
                        maxLength: 64
          /deux:
            get:
              operationId: lireDeux
              responses:
                '200':
                  description: Bon.
                  headers:
                    X-suivi:
                      description: Identifiant de suivi de la demande, toujours renseigne.
                      schema:
                        type: string
                        maxLength: 64
        """;

    private const string TwoHeaders = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /complet:
            get:
              operationId: lireComplet
              parameters:
                - name: X-appelant
                  in: header
                  required: true
                  schema:
                    type: string
                - name: X-suivi
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
            post:
              operationId: ecrireComplet
              parameters:
                - name: X-appelant
                  in: header
                  required: true
                  schema:
                    type: string
                - name: X-suivi
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
          /partiel:
            get:
              operationId: lirePartiel
              parameters:
                - name: X-appelant
                  in: header
                  required: true
                  schema:
                    type: string
                - name: X-suivi
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
            post:
              operationId: ecrirePartiel
              responses:
                '200':
                  description: Bon.
        """;

    private const string OnlyPartial = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /un:
            get:
              operationId: lireUn
              parameters:
                - name: X-appelant
                  in: header
                  required: true
                  schema:
                    type: string
                - name: X-suivi
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
            post:
              operationId: ecrireUn
              responses:
                '200':
                  description: Bon.
          /deux:
            get:
              operationId: lireDeux
              parameters:
                - name: X-appelant
                  in: header
                  required: true
                  schema:
                    type: string
                - name: X-suivi
                  in: header
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: Bon.
            post:
              operationId: ecrireDeux
              responses:
                '200':
                  description: Bon.
        """;

    private const string Deprecated = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /vieux:
            get:
              operationId: lireVieux
              description: Endpoint obsolète, remplacé par /neuf.
              responses:
                '200':
                  description: Bon.
        """;

    private const string DeprecatedFalse = """
        openapi: 3.0.3
        info:
          title: Essai
          version: 1.0.0
        paths:
          /vieux:
            get:
              operationId: lireVieux
              description: Endpoint obsolète, remplacé par /neuf.
              deprecated: false
              responses:
                '200':
                  description: Bon.
        """;
}
