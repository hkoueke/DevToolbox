using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>Ce que toutes les règles partagent : leur identifiant, ce qu'elles regardent, et le catalogue.</summary>
/// <remarks>
/// La gravité et la sûreté ne sont pas écrites dans chaque règle : elles viennent du catalogue, qui est
/// publié et qu'un essai compare à la spécification. Une règle qui les redirait pourrait les contredire.
/// </remarks>
public abstract class AuditRule : IAuditRule
{
    /// <summary>Crée la règle.</summary>
    /// <param name="ruleId">Son identifiant, qui fait partie de la sortie.</param>
    /// <param name="visits">Les sortes d'objets qu'elle regarde.</param>
    protected AuditRule(string ruleId, params SpecObjectKind[] visits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(visits);

        RuleId = ruleId;
        Visits = visits;
        Catalogue = RuleDefaults.For(ruleId);
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <inheritdoc />
    public IReadOnlyList<SpecObjectKind> Visits { get; }

    /// <summary>Ce que le catalogue dit de cette règle.</summary>
    protected RuleDefault Catalogue { get; }

    /// <inheritdoc />
    public abstract RuleReport Evaluate(AuditSubject subject);

    /// <summary>Compose un constat avec les valeurs du catalogue.</summary>
    /// <param name="message">Ce qu'il y a à dire.</param>
    /// <param name="location">Où.</param>
    /// <param name="action">Ce qu'il est recommandé de faire.</param>
    /// <param name="severity">La gravité, quand elle s'écarte du catalogue.</param>
    /// <param name="safety">La sûreté, quand la règle doit la dégrader.</param>
    /// <returns>Le constat.</returns>
    protected Finding Report(
        string message,
        SpecPointer location,
        string action,
        FindingSeverity? severity = null,
        SafetyClass? safety = null) =>
        new(
            RuleId,
            severity ?? Catalogue.Severity,
            safety ?? Catalogue.Safety,
            message,
            location,
            action);
}
