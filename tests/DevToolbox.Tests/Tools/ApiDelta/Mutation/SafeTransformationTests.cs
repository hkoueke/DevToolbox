using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tests.Tools.ApiDelta.Optimization;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Mutation;

/// <summary>
/// L'autre moitié de la matrice : aucune transformation que l'outil applique ne peut introduire un
/// changement cassant.
/// </summary>
/// <remarks>
/// Les deux garanties se tiennent ensemble. La première — aucun resserrement classé compatible — protège
/// le relecteur d'une confiance mal placée. La seconde, ici, protège la description elle-même : un
/// optimiseur qui resserrerait un contrat en croyant le remettre en forme produirait exactement le
/// changement que l'autre moitié existe pour détecter, mais sans qu'aucune comparaison n'ait lieu pour
/// le voir.
/// <para>
/// L'épreuve est la même pour toutes les mutations, et c'est ce qui en fait une garantie plutôt qu'une
/// collection de cas : ce qui a été appliqué est comparé à l'original, références résolues, sous le
/// profil du contrat. Une transformation qui aurait resserré quoi que ce soit échoue là, quelle que soit
/// la forme du resserrement.
/// </para>
/// </remarks>
public sealed class SafeTransformationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-sur-" + Guid.NewGuid().ToString("N"));

    public SafeTransformationTests() => Directory.CreateDirectory(_root);

    [Theory]
    [MemberData(nameof(BreakingMutationTests.Tightenings), MemberType = typeof(BreakingMutationTests))]
    public async Task No_transformation_applied_over_a_tightened_document_changes_its_contract(
        string description,
        string from,
        string to)
    {
        SpecDocument mutated = await MutatedAsync(description, from, to);

        OptimizationOutcome outcome = Applied(mutated);

        if (outcome.Applied.Count == 0)
        {
            // Rien d'applicable n'est un résultat légitime : ce qui est interdit est d'appliquer et de
            // changer le contrat.
            return;
        }

        outcome.Verification.ContractEquivalent.Should().BeTrue(
            "après « {0} », ce qui a été appliqué ne doit pas changer le contrat : {1}",
            description,
            outcome.Verification.Explanation);

        outcome.Verification.Established.Should().BeTrue(outcome.Verification.Explanation);
    }

    [Theory]
    [MemberData(nameof(BreakingMutationTests.Loosenings), MemberType = typeof(BreakingMutationTests))]
    public async Task No_transformation_applied_over_a_loosened_document_changes_its_contract(
        string description,
        string from,
        string to)
    {
        SpecDocument mutated = await MutatedAsync(description, from, to);

        OptimizationOutcome outcome = Applied(mutated);

        if (outcome.Applied.Count > 0)
        {
            outcome.Verification.ContractEquivalent.Should().BeTrue(
                "après « {0} » : {1}", description, outcome.Verification.Explanation);
        }
    }

    [Theory]
    [MemberData(nameof(BreakingMutationTests.Tightenings), MemberType = typeof(BreakingMutationTests))]
    public async Task A_safe_finding_over_a_tightened_document_is_still_a_proven_one(
        string description,
        string from,
        string to)
    {
        // Un constat se déclare corrigeable automatiquement si et seulement si sa sûreté est prouvée et
        // que l'équivalence a été établie sous le profil exigeant. La mutation ne doit pas pouvoir
        // transformer une ressemblance en preuve.
        SpecDocument mutated = await MutatedAsync(description, from, to);

        AuditResult audit = AuditHarness.Audit(mutated);

        audit.Findings.Should().OnlyContain(finding =>
            !finding.AutofixAvailable
            || (finding.Safety == SafetyClass.Safe && finding.StrictEquivalenceEstablished));
    }

    [Fact]
    public async Task An_applied_optimization_over_the_reference_description_changes_nothing_breaking()
    {
        // Le cas sans mutation, pour que l'essai dise quelque chose même quand la matrice est vide : la
        // description de référence, optimisée, comparée à elle-même.
        SpecDocument source = await ReadAsync("reference.yaml", CorpusLoader.Read("baseline-30.yaml"));

        OptimizationOutcome outcome = Applied(source);

        if (outcome.Applied.Count == 0)
        {
            return;
        }

        outcome.Verification.Established.Should().BeTrue(outcome.Verification.Explanation);

        // Et le comparateur, lui, voit des différences — c'est normal et c'est instructif. Il compare ce
        // qui est écrit, et une extraction remplace un objet par une référence à lui : la substitution
        // est un changement d'écriture. Prouver qu'une transformation ne casse rien demande de comparer
        // ce qui est signifié, références résolues, et c'est exactement pourquoi la vérification
        // d'équivalence de contrat existe comme mécanisme distinct plutôt que comme un appel au
        // comparateur.
        IReadOnlyList<Difference> differences = Compared(source, outcome);

        differences.Should().NotBeEmpty(
            "le comparateur voit la substitution : c'est lui qui a raison sur l'écriture, et la "
            + "vérification qui a raison sur le contrat");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Applique à une copie tout ce que le plan propose, y compris ce qui demande une relecture.</summary>
    /// <remarks>
    /// Y compris : l'épreuve porte sur ce que l'outil <em>peut</em> faire, pas sur ce qu'il fait par
    /// défaut. Une transformation à relire qui changerait le contrat serait un défaut même si personne ne
    /// l'autorise aujourd'hui.
    /// </remarks>
    [Fact]
    public async Task The_optimizer_applies_something_somewhere_over_the_mutation_corpus()
    {
        // Le garde-fou de l'essai voisin : celui-ci vérifie l'équivalence « si quelque chose a été
        // appliqué », et un optimiseur qui cesserait d'appliquer quoi que ce soit le laisserait vert en
        // n'affirmant plus rien. Il faut donc établir ailleurs que la machinerie est vivante — sans quoi
        // la théorie entière devient une suite d'assertions qui ne s'exécutent jamais.
        int applied = 0;

        foreach (object[] row in BreakingMutationTests.Loosenings.Select(data => data.ToArray()))
        {
            SpecDocument mutated = await MutatedAsync(
                (string)row[0], (string)row[1], (string)row[2]);

            applied += Applied(mutated).Applied.Count;
        }

        applied.Should().BeGreaterThan(
            0, "sans une seule transformation appliquée, l'essai d'équivalence n'affirme rien");
    }

    private static OptimizationOutcome Applied(SpecDocument document) =>
        OptimizationHarness.Applier().Apply(
            OptimizationHarness.Plan(document),
            document,
            new RunSelection(
                RunMode.Analyse,
                document.Path,
                string.Empty,
                "sortie",
                ApplySafeOptimizations: true,
                AllowedReviewRequiredRules:
                    ["OPT-002", "OPT-003", "OPT-004", "OPT-005", "OPT-007", "OPT-008"]),
            Ruleset.Default);

    /// <summary>Compare la copie optimisée à son original, comme un relecteur le ferait.</summary>
    private static IReadOnlyList<Difference> Compared(SpecDocument source, OptimizationOutcome outcome)
    {
        SpecReadResult optimised = new SpecReader(NullLogger<SpecReader>.Instance).ReadBytes(
            outcome.Serialized, "copie", DocumentRole.Candidate, LoadLimits.Default);

        optimised.IsFaithful.Should().BeTrue(
            string.Join(" · ", optimised.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return new DocumentComparer(new OpenApiMetamodel())
            .Compare(optimised.Document!, source, CancellationToken.None)
            .Value;
    }

    private Task<SpecDocument> MutatedAsync(string description, string from, string to)
    {
        string baseline = CorpusLoader.Read("baseline-30.yaml");

        baseline.Should().Contain(
            from, "la mutation « {0} » doit avoir prise sur la description de référence", description);

        int position = baseline.IndexOf(from, StringComparison.Ordinal);

        return ReadAsync("mutant.yaml", baseline.Remove(position, from.Length).Insert(position, to));
    }

    private async Task<SpecDocument> ReadAsync(string name, string content)
    {
        string path = Path.Combine(_root, name);
        await File.WriteAllTextAsync(path, content);

        SpecReadResult read = await new SpecReader(NullLogger<SpecReader>.Instance)
            .ReadAsync(path, DocumentRole.Production, LoadLimits.Default, CancellationToken.None);

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return read.Document!;
    }
}
