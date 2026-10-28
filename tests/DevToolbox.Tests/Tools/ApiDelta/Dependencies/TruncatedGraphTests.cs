using DevToolbox.Tests.Tools.ApiDelta.Audit;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Dependencies;

/// <summary>
/// Que disent les règles qui dépendent du graphe, lorsque la traversée s'est arrêtée en route ?
/// </summary>
/// <remarks>
/// Un graphe tronqué ressemble en tout point à un graphe complet : les arêtes qu'il porte sont justes,
/// et rien dans sa forme ne dit qu'il en manque. Les deux budgets qui l'arrêtent — la chaîne trop longue
/// et le nombre de références — ne produisaient qu'un avertissement, non bloquant, que les lecteurs du
/// graphe ignoraient. Chacun concluait donc depuis ce qu'il avait sous la main.
/// <para>
/// Le cas le plus coûteux est celui du composant : atteignable seulement au-delà du budget, il paraît
/// inaccessible, et la règle l'annonçait retirable sans risque. Supprimer un composant encore référencé
/// casse le document — c'est-à-dire exactement la perte que cet outil existe pour empêcher.
/// </para>
/// </remarks>
public sealed class TruncatedGraphTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-tronque-" + Guid.NewGuid().ToString("N"));

    public TruncatedGraphTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task No_component_is_called_removable_from_a_truncated_walk()
    {
        SpecDocument document = await AuditHarness.ReadAsync(_root, "baseline-30.yaml");

        AuditResult audit = AuditHarness.Audit(
            document, limits: LoadLimits.Default with { MaxReferences = 1 });

        RuleOutcome unused = audit.Outcomes.Should().ContainSingle(outcome =>
            outcome.RuleId == "AUD-003").Which;

        unused.State.Should().Be(
            RuleState.NotEvaluated,
            "un composant atteignable seulement au-delà du budget paraîtrait inaccessible");

        unused.FindingCount.Should().Be(0, "une règle qui renonce ne rend rien");
        unused.Reason.Should().Contain("budget");
    }

    [Fact]
    public async Task A_truncated_walk_does_not_let_the_silence_mean_no_dangling_reference()
    {
        SpecDocument document = await AuditHarness.ReadAsync(_root, "baseline-30.yaml");

        AuditResult audit = AuditHarness.Audit(
            document, limits: LoadLimits.Default with { MaxReferences = 1 });

        RuleOutcome dangling = audit.Outcomes.Should().ContainSingle(outcome =>
            outcome.RuleId == "AUD-002").Which;

        // Tronqué, et non « non évalué » : les références mortes que la traversée a vues avant de
        // s'arrêter sont de vrais constats, et les perdre serait aussi malhonnête que de laisser croire
        // qu'il n'y en a pas d'autres.
        dangling.State.Should().Be(RuleState.Truncated);
        dangling.Reason.Should().Contain("budget");
    }

    [Fact]
    public async Task A_complete_walk_leaves_both_rules_evaluated()
    {
        // La moitié qui empêche les deux refus de devenir des refus permanents.
        SpecDocument document = await AuditHarness.ReadAsync(_root, "baseline-30.yaml");

        AuditResult audit = AuditHarness.Audit(document);

        foreach (string ruleId in new[] { "AUD-002", "AUD-003" })
        {
            audit.Outcomes.Should().ContainSingle(outcome => outcome.RuleId == ruleId)
                .Which.State.Should().Be(
                    RuleState.Evaluated, ruleId + " doit s'évaluer sur un graphe complet");
        }

        audit.IsComplete.Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
