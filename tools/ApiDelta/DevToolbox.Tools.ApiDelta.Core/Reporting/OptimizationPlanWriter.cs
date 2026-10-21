using System.Globalization;
using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Optimization;

namespace DevToolbox.Tools.ApiDelta.Core.Reporting;

/// <summary>
/// Écrit le plan de nettoyage : un devis, groupé par classe de sûreté.
/// </summary>
/// <remarks>
/// Le plan ne nomme aucun fichier qu'il aurait écrit, parce qu'il n'en a écrit aucun. Il se lit comme un
/// devis — ce que chaque transformation remplace, ce qu'elle créerait, ce qui la justifie, ce qu'elle
/// rapporterait et comment la défaire — et il se relit tel quel après application, le manifeste disant
/// alors ce qui a effectivement été fait.
/// </remarks>
public static class OptimizationPlanWriter
{
    /// <summary>Écrit le plan.</summary>
    /// <param name="run">L'exécution achevée.</param>
    /// <returns>Le plan, en octets.</returns>
    public static ReadOnlyMemory<byte> Write(ApiDeltaRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        OptimizationPlan plan = run.Plan ?? OptimizationPlan.Empty;

        StringBuilder builder = new();

        builder.AppendLine("# Chiffrage du nettoyage. Rien n'a été modifié : ce fichier est un devis.");
        builder.Append("production: ").AppendLine(Quote(run.Production.Path));
        builder.Append("productionBytes: ").AppendLine(Number(run.Production.ByteLength));
        builder.Append("estimatedSaving: ").AppendLine(Number(plan.TotalEstimatedSaving));

        Growth(builder, plan);
        Proposals(builder, "safe", plan.Safe);
        Proposals(builder, "reviewRequired", plan.ReviewRequired);
        Groups(builder, plan);

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void Growth(StringBuilder builder, OptimizationPlan plan)
    {
        builder.AppendLine("growth:");
        builder.Append("  projectedBytes: ").AppendLine(Number(plan.Growth.ProjectedBytes));
        builder.Append("  limit: ")
            .AppendLine(plan.Growth.Limit is { } limit ? Number(limit) : "null");
        builder.Append("  exceeded: ").AppendLine(plan.Growth.Exceeded ? "true" : "false");
    }

    /// <summary>Les propositions d'une classe de sûreté.</summary>
    /// <remarks>
    /// Groupées par sûreté, et non par règle : le lecteur décide d'abord ce qu'il laisse l'outil faire,
    /// et seulement ensuite ce qu'il veut relire lui-même.
    /// </remarks>
    private static void Proposals(
        StringBuilder builder,
        string heading,
        IEnumerable<Transformation> proposals)
    {
        IReadOnlyList<Transformation> ordered =
        [
            .. proposals
                .OrderByDescending(proposal => proposal.EstimatedBytesSaved)
                .ThenBy(proposal => proposal.RuleId, StringComparer.Ordinal),
        ];

        builder.Append(heading).AppendLine(":");

        if (ordered.Count == 0)
        {
            builder.AppendLine("  []");
            return;
        }

        foreach (Transformation proposal in ordered)
        {
            Proposal(builder, proposal);
        }
    }

    private static void Proposal(StringBuilder builder, Transformation proposal)
    {
        builder.Append("  - ruleId: ").AppendLine(proposal.RuleId);
        builder.Append("    state: ").AppendLine(proposal.State.ToString().ToLowerInvariant());
        builder.Append("    from: ").AppendLine(proposal.FromRuleId ?? "null");
        builder.Append("    summary: ").AppendLine(Quote(proposal.Summary));
        builder.Append("    origins: [").Append(
            string.Join(", ", proposal.Origins.Select(origin => Quote(origin.Json)))).AppendLine("]");
        builder.Append("    destination: ")
            .AppendLine(proposal.Destination is { } destination ? Quote(destination.Json) : "null");
        builder.Append("    evidence: ")
            .AppendLine(proposal.Evidence is { } evidence ? Quote("sha256:" + evidence) : "null");
        builder.Append("    estimatedSaving: ").AppendLine(Number(proposal.EstimatedBytesSaved));
        builder.Append("    breakEven: ")
            .AppendLine(proposal.BreakEvenOccurrences is { } breakEven
                ? Number(breakEven)
                : "null");
        builder.Append("    reversal: ").AppendLine(Quote(proposal.Reversal.Text));

        if (proposal.ReviewReason is { } reason)
        {
            builder.Append("    reviewReason: ").AppendLine(Quote(reason));
        }
    }

    /// <summary>
    /// Les répétitions mesurées, y compris celles qu'aucune proposition ne couvre.
    /// </summary>
    /// <remarks>
    /// Y compris, parce que c'est là que se lit la différence entre « le fichier est propre » et
    /// « l'outil ne sait pas traiter ce qu'il a trouvé ».
    /// </remarks>
    private static void Groups(StringBuilder builder, OptimizationPlan plan)
    {
        builder.AppendLine("redundancyGroups:");

        if (plan.Groups.Count == 0)
        {
            builder.AppendLine("  []");
            return;
        }

        foreach (RedundancyGroup group in plan.Groups)
        {
            builder.Append("  - ruleId: ").AppendLine(group.RuleId);
            builder.Append("    kind: ").AppendLine(CandidateKindNames.Of(group.Kind));
            builder.Append("    tier: ").AppendLine(group.Tier.ToString().ToLowerInvariant());
            builder.Append("    occurrences: ").AppendLine(Number(group.Count));
            builder.Append("    currentBytes: ").AppendLine(Number(group.Savings.CurrentBytes));
            builder.Append("    projectedBytes: ").AppendLine(Number(group.Savings.ProjectedBytes));
            builder.Append("    absoluteSaving: ").AppendLine(Number(group.Savings.AbsoluteSaving));
            builder.Append("    percentSaving: ").AppendLine(Percent(group.Savings.PercentSaving));
            builder.Append("    breakEven: ")
                .AppendLine(group.Savings.BreakEvenOccurrences is { } breakEven
                    ? Number(breakEven)
                    : "null");
            builder.Append("    recommended: ")
                .AppendLine(group.Savings.Recommended ? "true" : "false");
            builder.Append("    location: ").AppendLine(Quote(group.Location.Json));
        }
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Percent(double value) => value.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>
    /// Entre apostrophes, en doublant celles du contenu.
    /// </summary>
    /// <remarks>
    /// Tout est cité, y compris ce qui n'en aurait pas besoin. Une écriture qui décide au cas par cas si
    /// une citation est nécessaire finit par se tromper sur un pointeur contenant deux-points, et produit
    /// un fichier qui ne se relit pas.
    /// </remarks>
    private static string Quote(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
