namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce que l'audit a trouvé, et ce qu'il n'a pas pu regarder.</summary>
/// <param name="Findings">Les constats, groupés et triés.</param>
/// <param name="Outcomes">Le sort de chaque règle enregistrée, sans exception.</param>
/// <remarks>
/// Les deux listes vont ensemble, et l'invariant est que la seconde est complète : une règle enregistrée
/// y figure, qu'elle ait trouvé quelque chose, rien, ou qu'elle n'ait pas pu s'exécuter. Un audit sans
/// constat n'est une bonne nouvelle que si toutes les règles ont tourné, et c'est la seule façon de
/// distinguer les deux.
/// </remarks>
public sealed record AuditResult(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<RuleOutcome> Outcomes)
{
    /// <summary>Un audit qui n'a pas eu lieu.</summary>
    public static AuditResult Empty { get; } = new([], []);

    /// <summary>
    /// Ce que les sourdines ont changé, et ce qu'elles n'ont pas trouvé.
    /// </summary>
    /// <remarks>
    /// <see cref="Findings"/> ne porte que les constats actifs, et <see cref="RuleOutcome.FindingCount"/>
    /// ce que la règle a trouvé. Les deux peuvent donc différer, et c'est voulu : la règle rend compte de
    /// son travail, la liste rend compte de ce qui reste à traiter, et la section des sourdines explique
    /// l'écart. Masquer l'un des deux rendrait un fichier de sourdines invérifiable.
    /// </remarks>
    public Configuration.SuppressionOutcome Suppressions { get; init; } =
        Configuration.SuppressionOutcome.None;

    /// <summary>Indique si chaque règle a pu être évaluée.</summary>
    public bool IsComplete => Outcomes.All(outcome => outcome.State != RuleState.NotEvaluated);

    /// <summary>Les règles qui n'ont pas pu être évaluées.</summary>
    public IEnumerable<RuleOutcome> NotEvaluated =>
        Outcomes.Where(outcome => outcome.State == RuleState.NotEvaluated);

    /// <summary>Les règles qui ont rendu moins que ce qu'elles ont trouvé.</summary>
    public IEnumerable<RuleOutcome> Truncated =>
        Outcomes.Where(outcome => outcome.State == RuleState.Truncated);
}
