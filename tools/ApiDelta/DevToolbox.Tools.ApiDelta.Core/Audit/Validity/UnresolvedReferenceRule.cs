using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Validity;

/// <summary>
/// Rapporte chaque référence qui ne désigne rien, et chacun des endroits qui l'emploie.
/// </summary>
/// <remarks>
/// Chacun des endroits, et non le premier : une référence morte écrite à vingt endroits est un seul défaut
/// mais vingt corrections, et nommer un seul emplacement obligerait le lecteur à chercher les dix-neuf
/// autres.
/// </remarks>
public sealed class UnresolvedReferenceRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public UnresolvedReferenceRule()
        : base("AUD-002", SpecObjectKind.Document)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.References is not { } references)
        {
            return RuleReport.NotEvaluated(
                "Les références n'ont pas été résolues : la règle ne peut pas dire qu'elles aboutissent.");
        }

        return RuleReport.Evaluated(
        [
            .. references.Unresolved
                .GroupBy(edge => edge.To.ToString(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Dangling(group.Key, [.. group])),
        ]);
    }

    private Finding Dangling(string target, IReadOnlyList<ReferenceEdge> uses)
    {
        IReadOnlyList<SpecPointer> locations = [.. uses.Select(edge => edge.From)];

        return Report(
            uses.Count == 1
                ? $"La référence « {target} » ne désigne rien."
                : $"La référence « {target} » ne désigne rien, et est employée {uses.Count} fois.",
            locations[0],
            "Rétablir la cible, ou corriger la référence.") with
        {
            Locations = locations,
            Evidence = new FindingEvidence(Occurrences: uses.Count),
        };
    }
}
