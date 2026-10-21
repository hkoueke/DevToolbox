using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte les identifiants d'opération manquants et ceux employés deux fois.
/// </summary>
/// <remarks>
/// Deux gravités, parce que les deux défauts n'ont pas la même conséquence. Un identifiant manquant laisse
/// un client engendré inventer un nom, ce qui est désagréable ; deux opérations sous le même identifiant
/// empêchent d'engendrer quoi que ce soit, ce qui est une erreur.
/// </remarks>
public sealed class OperationIdRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public OperationIdRule()
        : base("AUD-005", SpecObjectKind.Operation)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IReadOnlyList<AuditNode> operations = subject.Nodes(SpecObjectKind.Operation);

        return RuleReport.Evaluated([.. Absent(operations), .. Repeated(operations)]);
    }

    private IEnumerable<Finding> Absent(IEnumerable<AuditNode> operations) =>
        operations
            .Where(operation => string.IsNullOrWhiteSpace(
                Nodes.Text(operation.Node, "operationId")))
            .Select(operation => Report(
                "L'opération ne porte pas d'identifiant.",
                operation.Location,
                "Nommer l'opération par un « operationId » stable.",
                FindingSeverity.Warning));

    private IEnumerable<Finding> Repeated(IEnumerable<AuditNode> operations) =>
        operations
            .Select(operation => (operation, Id: Nodes.Text(operation.Node, "operationId")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Id))
            .GroupBy(pair => pair.Id!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Duplicate(group.Key, [.. group.Select(pair => pair.operation.Location)]));

    private Finding Duplicate(string id, IReadOnlyList<SpecPointer> locations) =>
        Report(
            $"L'identifiant « {id} » désigne {locations.Count} opérations.",
            locations[0],
            "Donner à chaque opération un identifiant qui n'appartient qu'à elle.",
            FindingSeverity.Error) with
        {
            Locations = locations,
            Evidence = new FindingEvidence(Occurrences: locations.Count),
        };
}
