using DevToolbox.Tools.ApiDelta.Core.Configuration;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Quelles règles d'optimisation sont actives à défaut de réglage.
/// </summary>
/// <remarks>
/// Une seule table, lue par le plan comme par l'application. Deux listes auraient fini par diverger :
/// le plan aurait proposé ce que l'application refusait, ou — bien pire — l'application aurait appliqué
/// ce que personne n'avait demandé.
/// <para>
/// Deux règles sont inactives par défaut, et pour la même raison de fond : elles changent ce que la
/// description publie. Extraire un schéma écrit sur place lui donne un nom, qui devient un type public
/// chez chaque appelant ; poser un drapeau de dépréciation fait apparaître un avertissement de
/// compilation chez chacun d'eux. Les deux sont souhaitables et aucune ne doit arriver sans demande.
/// </para>
/// </remarks>
public static class OptimizationDefaults
{
    private static readonly HashSet<string> Inactive = new(StringComparer.Ordinal)
    {
        "OPT-006",
        "OPT-009",
    };

    /// <summary>Indique si une règle est active à défaut de réglage.</summary>
    /// <param name="ruleId">L'identifiant de la règle.</param>
    /// <returns><see langword="false"/> pour les règles qu'il faut demander.</returns>
    public static bool EnabledByDefault(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        return !Inactive.Contains(ruleId);
    }

    /// <summary>Indique si une règle est active, le réglage de l'équipe primant le défaut.</summary>
    /// <param name="ruleId">L'identifiant de la règle.</param>
    /// <param name="ruleset">Ce que l'équipe a décidé.</param>
    /// <returns><see langword="true"/> si la règle peut proposer et appliquer.</returns>
    public static bool Enabled(string ruleId, Ruleset ruleset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(ruleset);

        return ruleset.Rules.TryGetValue(ruleId, out RuleSetting? setting)
            ? setting.Enabled
            : EnabledByDefault(ruleId);
    }
}
