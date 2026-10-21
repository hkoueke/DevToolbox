using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les manquements à la structure de la version déclarée.
/// </summary>
/// <remarks>
/// La règle ne valide rien elle-même : elle reprend les verdicts de la validation structurelle. Écrire une
/// seconde validation à côté de la première reviendrait à entretenir deux avis sur la même question.
/// </remarks>
public sealed class InvalidStructureRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public InvalidStructureRule()
        : base("AUD-001", SpecObjectKind.Document)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.StructuralVerdicts is not { } verdicts)
        {
            return RuleReport.NotEvaluated(
                "La structure n'a pas été validée : la règle ne peut pas dire que la description la respecte.");
        }

        return RuleReport.Evaluated(
        [
            .. verdicts
                .Where(verdict => string.Equals(
                    verdict.Code, DiagnosticCodes.StructuralViolation, StringComparison.Ordinal))
                .Select(verdict => Report(
                    verdict.Message,
                    verdict.Location ?? SpecPointer.Root,
                    "Corriger la structure pour qu'elle respecte la version déclarée.")),
        ]);
    }
}
