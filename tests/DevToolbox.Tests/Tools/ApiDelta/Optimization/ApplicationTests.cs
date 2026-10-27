using DevToolbox.Domain.Results;
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
/// Ce que l'application fait, et les trois choses qu'elle refuse de faire.
/// </summary>
public sealed class ApplicationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-application-" + Guid.NewGuid().ToString("N"));

    public ApplicationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task A_verified_copy_carries_the_bytes_and_says_what_it_established()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");

        OptimizationOutcome outcome = OptimizationHarness.Applier().Apply(
            OptimizationHarness.Plan(document), document, Applying(), Ruleset.Default);

        outcome.Applied.Should().NotBeEmpty();
        outcome.Verification.Established.Should().BeTrue(outcome.Verification.Explanation);
        outcome.Publishable.Should().BeTrue();
        outcome.Serialized.Length.Should().BeGreaterThan(0);

        outcome.Applied.Should().OnlyContain(
            transformation => transformation.State == TransformationState.Applied);
    }

    [Fact]
    public async Task The_copy_is_smaller_than_the_original_and_still_says_the_same_thing()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");

        OptimizationOutcome outcome = OptimizationHarness.Applier().Apply(
            OptimizationHarness.Plan(document), document, Applying(), Ruleset.Default);

        outcome.Serialized.Length.Should().BeLessThan((int)document.ByteLength);
        outcome.Verification.ContractEquivalent.Should().BeTrue();
    }

    [Fact]
    public async Task An_injected_contract_change_publishes_nothing_and_names_what_changed()
    {
        // La vérification porte sur les octets, non sur l'arbre : une règle qui retire une réponse
        // produit un arbre que l'outil croit bon, et des octets qui disent autre chose.
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");

        OptimizationOutcome outcome = OptimizationHarness
            .Applier([new ContractBreakingRule()])
            .Apply(Breaking(), document, Applying(), Ruleset.Default);

        outcome.Applied.Should().HaveCount(1, "la transformation a bien été appliquée à l'arbre");
        outcome.Verification.Established.Should().BeFalse();
        outcome.Verification.ContractEquivalent.Should().BeFalse();
        outcome.Publishable.Should().BeFalse();

        outcome.Serialized.Length.Should().Be(0, "aucun octet ne doit être disponible à l'écriture");
        outcome.Verification.Differences.Should().NotBeEmpty();
        outcome.Verification.Explanation.Should().Contain("le contrat a changé");
    }

    [Fact]
    public async Task A_review_required_transformation_is_not_applied_without_its_rule_identifier()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");

        SpecPointer component = SpecPointer.Root
            .Child("components").Child("schemas").Child("Inaccessible");

        OptimizationPlan plan = Single(new Transformation(
            "OPT-007",
            SafetyClass.ReviewRequired,
            [component],
            Destination: null,
            Evidence: null,
            new ReversalDescription("réinsérer le composant", Reversible: true),
            TransformationState.Proposed));

        OptimizationOutcome refused = OptimizationHarness.Applier().Apply(
            plan, document, Applying(), Ruleset.Default);

        refused.Applied.Should().BeEmpty();
        refused.Declined.Should().ContainSingle()
            .Which.Reason.Should().Contain("n'a pas été autorisée");

        // Nommément autorisée, elle s'applique.
        OptimizationOutcome allowed = OptimizationHarness.Applier().Apply(
            plan, document, Applying(["OPT-007"]), Ruleset.Default);

        allowed.Applied.Should().ContainSingle()
            .Which.RuleId.Should().Be("OPT-007");
    }

    [Fact]
    public async Task Two_remedies_for_the_same_repetition_do_not_both_apply_and_the_proven_one_wins()
    {
        // Extraire chaque paramètre répété et remonter le jeu d'en-têtes au niveau du chemin sont des
        // alternatives. La seconde appliquée après la première ne trouverait plus que des références :
        // l'une des deux doit céder, et c'est celle que l'outil ne sait pas prouver.
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Reviewable);
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        plan.ReviewRequired.Should().NotBeEmpty("le document porte un cas à relire");

        OptimizationOutcome outcome = OptimizationHarness.Applier().Apply(
            plan, document, Applying(["OPT-008"]), Ruleset.Default);

        outcome.Applied.Should().OnlyContain(transformation => transformation.RuleId == "OPT-002");

        outcome.Declined.Should().Contain(declined =>
            declined.Transformation.RuleId == "OPT-008"
            && declined.Reason.Contains("ne se cumulent pas", StringComparison.Ordinal));

        outcome.Verification.Established.Should().BeTrue(outcome.Verification.Explanation);
    }

    [Fact]
    public async Task An_unsafe_transformation_is_never_applied_whatever_was_allowed()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");

        OptimizationPlan plan = OptimizationHarness.Plan(document);

        Transformation unsafeOne = OptimizationHarness.Of(plan, "OPT-002") with
        {
            Safety = SafetyClass.Unsafe,
        };

        OptimizationOutcome outcome = OptimizationHarness.Applier().Apply(
            plan with { Transformations = [unsafeOne] },
            document,
            Applying(["OPT-002"]),
            Ruleset.Default);

        outcome.Applied.Should().BeEmpty();
        outcome.Declined.Should().ContainSingle()
            .Which.Reason.Should().Contain("seulement signalée");
    }

    [Fact]
    public async Task An_object_carrying_a_protected_extension_is_left_alone()
    {
        // L'outil ne sait pas ce qu'une passerelle lit. Déplacer cette donnée serait affirmer une
        // équivalence qu'il ne peut pas établir.
        SpecDocument document = await OptimizationHarness.WrittenAsync(_root, Guarded);
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        plan.Transformations.Should().NotBeEmpty("la répétition est réelle et chiffrée");

        OptimizationOutcome outcome = OptimizationHarness.Applier().Apply(
            plan, document, Applying(), Ruleset.Default);

        outcome.Applied.Should().BeEmpty();
        outcome.Declined.Should().Contain(declined =>
            declined.Reason.Contains("ne sait pas évaluer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_rule_no_one_registered_is_refused_by_name()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        OptimizationOutcome outcome = OptimizationHarness.Applier([]).Apply(
            plan, document, Applying(), Ruleset.Default);

        outcome.Applied.Should().BeEmpty();
        outcome.Declined.Should().NotBeEmpty();
        outcome.Declined[0].Reason.Should().Contain("Aucune règle enregistrée");
    }

    [Fact]
    public async Task Two_applications_of_the_same_plan_produce_the_same_bytes()
    {
        SpecDocument document = await OptimizationHarness.CorpusAsync(_root, "redundant.yaml");
        OptimizationPlan plan = OptimizationHarness.Plan(document);

        OptimizationOutcome first = OptimizationHarness.Applier().Apply(
            plan, document, Applying(), Ruleset.Default);
        OptimizationOutcome second = OptimizationHarness.Applier().Apply(
            plan, document, Applying(), Ruleset.Default);

        second.Serialized.ToArray().Should().Equal(first.Serialized.ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RunSelection Applying(IReadOnlyList<string>? allowed = null) =>
        new(
            RunMode.Analyse,
            "production.yaml",
            string.Empty,
            "sortie",
            ApplySafeOptimizations: true,
            AllowedReviewRequiredRules: allowed);

    /// <summary>Un plan d'une seule transformation.</summary>
    private static OptimizationPlan Single(Transformation transformation) =>
        new([], [transformation], GrowthCheck.For(12920, 0, 0));

    /// <summary>Un plan d'une seule transformation, confiée à la règle qui casse le contrat.</summary>
    private static OptimizationPlan Breaking() =>
        Single(new Transformation(
            "OPT-999",
            SafetyClass.Safe,
            [SpecPointer.Root.Child("paths").Child("/ressource1").Child("get")],
            Destination: null,
            Evidence: "abc",
            new ReversalDescription("remettre la réponse", Reversible: true),
            TransformationState.Proposed));

    /// <summary>
    /// Une règle qui retire une réponse. Elle existe pour que l'essai puisse établir ce que la
    /// vérification refuse, ce qu'aucune règle réelle ne permet de faire.
    /// </summary>
    private sealed class ContractBreakingRule : IOptimizationRule
    {
        public string RuleId => "OPT-999";

        public bool EnabledByDefault => true;

        public bool Removes => false;

        public Result<SpecNode> Apply(SpecNode root, Transformation transformation) =>
            Result.Success(
                TreeEdits.Remove(
                    root, transformation.Origins[0].Child("responses").Child("400")));
    }

    private const string Reviewable = """
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
        """;

    private const string Guarded = """
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
                  description: Le service appelant, tel que la passerelle le lit.
                  x-sg-passerelle:
                    catalogue: essai
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
                  description: Le service appelant, tel que la passerelle le lit.
                  x-sg-passerelle:
                    catalogue: essai
                  schema:
                    type: string
                    maxLength: 32
              responses:
                '200':
                  description: Bon.
        """;
}
