using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Governance;

/// <summary>
/// Rapporte les opérations qui remplacent ou annulent la sécurité du document.
/// </summary>
/// <remarks>
/// Une exigence de sécurité vide — <c>security: []</c> sur une opération — ouvre cette opération à tout le
/// monde. C'est parfois exactement ce qu'il faut, pour une sonde ou une page d'accueil ; c'est aussi la
/// façon la plus discrète de laisser une porte ouverte. L'outil ne tranche pas : il s'assure que personne
/// ne l'a fait sans le savoir.
/// </remarks>
public sealed class SecurityOverrideRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public SecurityOverrideRule()
        : base("AUD-302", SpecObjectKind.Operation)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        bool global = Nodes.Sequence(subject.Root, "security") is { Items.Count: > 0 };
        List<Finding> findings = [];

        foreach (AuditNode operation in subject.Nodes(SpecObjectKind.Operation))
        {
            if (Nodes.Sequence(operation.Node, "security") is not { } own)
            {
                continue;
            }

            findings.Add(own.Items.Count == 0
                ? Disabled(operation, global)
                : Overridden(operation, global, own));
        }

        return RuleReport.Evaluated(findings);
    }

    private Finding Disabled(AuditNode operation, bool global) =>
        Report(
            global
                ? "L'opération annule la sécurité du document par une exigence vide."
                : "L'opération déclare une exigence de sécurité vide.",
            operation.Location,
            "Confirmer que cette opération doit être ouverte sans authentification.");

    private Finding Overridden(AuditNode operation, bool global, SequenceNode own) =>
        Report(
            global
                ? $"L'opération remplace la sécurité du document par {own.Items.Count} exigence(s) propre(s)."
                : $"L'opération déclare {own.Items.Count} exigence(s) de sécurité que le document ne "
                    + "déclare pas.",
            operation.Location,
            "Vérifier que le remplacement est voulu : il ne se lit pas depuis le niveau du document.");
}
