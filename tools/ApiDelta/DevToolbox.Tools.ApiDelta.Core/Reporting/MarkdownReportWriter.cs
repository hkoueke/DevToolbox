using System.Globalization;
using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Reporting;

/// <summary>
/// Écrit le rapport lisible.
/// </summary>
/// <remarks>
/// Les sections sont écrites dans un ordre fixe et <em>toutes</em> présentes, même vides : une section
/// absente ne se distingue pas d'une section oubliée, et un lecteur qui ne trouve pas « changements
/// cassants » ne sait pas s'il n'y en a pas ou si l'outil n'a pas regardé.
/// <para>
/// Les métadonnées d'exécution — l'identifiant, l'heure, la durée, les chemins — sont rassemblées dans un
/// bloc délimité, et nulle part ailleurs. C'est ce qui permet au reste d'être identique d'une exécution à
/// l'autre et de pouvoir être comparé octet pour octet.
/// </para>
/// </remarks>
public static class MarkdownReportWriter
{
    /// <summary>Le marqueur ouvrant du bloc de métadonnées.</summary>
    public const string MetadataOpen = "<!-- apidelta:run-metadata";

    /// <summary>Le marqueur fermant du bloc de métadonnées.</summary>
    public const string MetadataClose = "-->";

    /// <summary>Écrit le rapport de comparaison.</summary>
    /// <param name="run">L'exécution achevée.</param>
    /// <returns>Le rapport, en octets.</returns>
    public static ReadOnlyMemory<byte> Write(ApiDeltaRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        StringBuilder builder = new();

        Metadata(builder, run);
        Summary(builder, run);
        Inputs(builder, run);
        Validation(builder, run);
        Version(builder, run);
        Changes(builder, run);
        Findings(builder, run);
        Redundancy(builder, run);
        Proposals(builder, run);
        Suppressed(builder, run);
        Artifacts(builder, run);
        Limitations(builder, run);
        Carried(builder, run);
        Correspondence(builder, run);
        Coverage(builder, run);
        Comparability(builder, run);

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Écrit le rapport d'examen du fichier de production.
    /// </summary>
    /// <param name="run">L'exécution achevée.</param>
    /// <returns>Le rapport, en octets.</returns>
    /// <remarks>
    /// Un rapport à part, et les mêmes numéros de section que dans le rapport de comparaison : ce sont les
    /// mêmes sections, et un lecteur qui connaît l'une retrouve l'autre. Rien de la comparaison n'y entre —
    /// un constat d'examen mêlé aux changements de contrat obligerait le relecteur à trier deux sujets dans
    /// une même liste, et c'est ainsi qu'on finit par ne plus lire ni l'un ni l'autre.
    /// </remarks>
    public static ReadOnlyMemory<byte> WriteAudit(ApiDeltaRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        StringBuilder builder = new();

        Metadata(builder, run);
        builder.AppendLine("# Examen du fichier OpenAPI en service").AppendLine();

        Inputs(builder, run);
        Validation(builder, run);
        Findings(builder, run);
        Redundancy(builder, run);
        Proposals(builder, run);
        Suppressed(builder, run);
        Artifacts(builder, run);
        Limitations(builder, run);

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void Metadata(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine(MetadataOpen);
        builder.Append("runId: ").AppendLine(run.RunId.ToString());
        builder.Append("startedAt: ")
            .AppendLine(run.StartedAt.ToString("O", CultureInfo.InvariantCulture));
        builder.Append("durationMs: ")
            .AppendLine(((long)run.Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
        builder.Append("production: ").AppendLine(run.Production.Path);

        if (run.Candidate is { } candidate)
        {
            builder.Append("candidate: ").AppendLine(candidate.Path);
        }

        builder.AppendLine(MetadataClose).AppendLine();
    }

    private static void Summary(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("# Rapport de comparaison OpenAPI").AppendLine();
        builder.AppendLine("## 1. Résumé").AppendLine();

        builder.Append("- Mode : ").AppendLine(run.Selection.Mode.ToString());
        builder.Append("- Différences : ")
            .AppendLine(run.Differences.Count.ToString(CultureInfo.InvariantCulture));

        foreach (ChangeImpact impact in new[]
        {
            ChangeImpact.Breaking, ChangeImpact.NonBreaking,
            ChangeImpact.DocumentationOnly, ChangeImpact.Unknown,
        })
        {
            builder.Append("  - ").Append(Name(impact)).Append(" : ")
                .AppendLine(run.Differences
                    .Count(difference => difference.Impact == impact)
                    .ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("- Diagnostics : ")
            .AppendLine(run.Diagnostics.Count.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine();
    }

    private static void Inputs(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 2. Documents lus").AppendLine();
        builder.AppendLine("| Rôle | Fichier | Forme | Version | Octets | Empreinte |");
        builder.AppendLine("|------|---------|-------|---------|--------|-----------|");

        Row(builder, run.Production);

        if (run.Candidate is { } candidate)
        {
            Row(builder, candidate);
        }

        builder.AppendLine();
    }

    private static void Row(StringBuilder builder, SpecDocument document) =>
        builder.Append("| ").Append(document.Role)
            .Append(" | ").Append(Path.GetFileName(document.Path))
            .Append(" | ").Append(document.Format)
            .Append(" | ").Append(document.DeclaredVersion)
            .Append(" | ").Append(document.ByteLength.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(Fingerprint(document))
            .AppendLine(" |");

    /// <summary>
    /// L'empreinte d'un document lu, ou ce qui en tient lieu quand il n'y en a pas.
    /// </summary>
    /// <remarks>
    /// Un document projeté depuis un contrat de service n'a pas d'empreinte : il n'est le contenu d'aucun
    /// fichier, et lui en inventer une désignerait quelque chose qui n'existe pas. Le dire est la seule
    /// réponse honnête — et la seule qui ne fasse pas tomber le rapport, ce que la découpe des douze
    /// premiers caractères faisait sur une chaîne vide.
    /// </remarks>
    private static string Fingerprint(SpecDocument document) => document.ContentHash.Length >= 12
        ? document.ContentHash[..12]
        : "sans empreinte : document projeté, et non lu d'un fichier";

    private static void Validation(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 3. Validation").AppendLine();

        if (run.Diagnostics.Count == 0)
        {
            builder.AppendLine("Rien à signaler.").AppendLine();
            return;
        }

        foreach (Diagnostic diagnostic in run.Diagnostics
            .OrderBy(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Position?.Line ?? 0))
        {
            builder.Append("- **").Append(diagnostic.Severity).Append("** `")
                .Append(diagnostic.Code).Append("` ");

            if (diagnostic.Position is { } position)
            {
                builder.Append('(').Append(position.ToString()).Append(") ");
            }

            builder.AppendLine(diagnostic.Message);
        }

        builder.AppendLine();
    }

    private static void Version(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 4. Version recommandée").AppendLine();

        if (run.Version is not { } version)
        {
            builder.AppendLine("Sans objet : ce mode ne compare pas deux descriptions.").AppendLine();
            return;
        }

        if (version.BaselineProblem is { } problem)
        {
            builder.Append("**Point de départ illisible.** ").AppendLine(problem).AppendLine();
        }

        builder.Append("- Point de départ : ")
            .AppendLine(version.Baseline?.ToString() ?? "indéterminé");
        builder.Append("- Incrément : ").AppendLine(Increment(version.Increment));
        builder.Append("- Recommandée : ")
            .AppendLine(version.Recommended?.ToString() ?? "indéterminée");

        if (version.Override is { } imposed)
        {
            builder.Append("- Version imposée : ").AppendLine(imposed.Requested.ToString());

            if (imposed.BelowRecommendation)
            {
                builder.AppendLine(
                    "  - **Cette version est inférieure à ce que les changements commandent.** "
                    + (imposed.Forced
                        ? "Le développeur a confirmé vouloir passer outre."
                        : "Elle demande une confirmation explicite."));
            }
        }

        Contributors(builder, version);

        builder.AppendLine("### Politique en vigueur").AppendLine();

        foreach ((string key, Configuration.VersionImpact impact) in version.ResolvedPolicy
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            builder.Append("- `").Append(key).Append("` : ").AppendLine(Increment(impact));
        }

        builder.AppendLine();
    }

    private static void Contributors(StringBuilder builder, Versioning.VersionRecommendation version)
    {
        if (version.Contributors.Count > 0)
        {
            builder.AppendLine().AppendLine("### Ce qui a commandé cet incrément").AppendLine();

            foreach (Versioning.VersionContributor contributor in version.Contributors)
            {
                builder.Append("- `").Append(contributor.ChangeId).Append("` `")
                    .Append(contributor.Location.Json).AppendLine("`");
            }
        }

        if (!version.RequiresReview)
        {
            builder.AppendLine();
            return;
        }

        // Dit, et non tu : une recommandation qui dépend de changements non classés ne vaut qu'autant que
        // ces changements auront été relus.
        builder.AppendLine()
            .AppendLine("### À relire avant de publier")
            .AppendLine()
            .AppendLine(
                "L'effet des changements suivants n'a pas pu être établi. La recommandation ne descend "
                + "pas en dessous d'un incrément mineur à cause d'eux, et elle ne vaut qu'autant qu'ils "
                + "auront été relus.")
            .AppendLine();

        foreach (Versioning.VersionContributor unreviewed in version.Unreviewed)
        {
            builder.Append("- `").Append(unreviewed.ChangeId).Append("` `")
                .Append(unreviewed.Location.Json).AppendLine("`");
        }

        builder.AppendLine();
    }

    private static string Increment(Configuration.VersionImpact impact) => impact switch
    {
        Configuration.VersionImpact.Major => "majeur",
        Configuration.VersionImpact.Minor => "mineur",
        Configuration.VersionImpact.Patch => "correctif",
        _ => "aucun",
    };

    private static void Changes(StringBuilder builder, ApiDeltaRun run)
    {
        Section(builder, "5. Changements cassants", run, difference =>
            difference.Impact == ChangeImpact.Breaking);

        Section(builder, "6. Opérations ajoutées", run, difference =>
            difference.Kind == DifferenceKind.Added && IsOperation(difference));

        Section(builder, "7. Opérations retirées", run, difference =>
            difference.Kind == DifferenceKind.Removed && IsOperation(difference));

        Section(builder, "8. Opérations modifiées", run, difference =>
            difference.Kind == DifferenceKind.Modified && IsUnderPaths(difference));

        Section(builder, "9. Changements de composants", run, difference =>
            difference.Location.Json.StartsWith("/components", StringComparison.Ordinal));

        Section(builder, "10. Changements de sécurité", run, difference =>
            string.Equals(difference.ChangeId, ChangeIds.SecurityChanged, StringComparison.Ordinal));

        Section(builder, "11. Changements d'extensions de fournisseur", run, difference =>
            difference.Location.Json.Contains("/x-", StringComparison.Ordinal));
    }

    /// <summary>
    /// Les constats de l'examen, groupés par règle.
    /// </summary>
    /// <remarks>
    /// Par règle, parce que c'est la décision à prendre qui est l'unité utile : quatorze occurrences d'un
    /// même en-tête se tranchent une fois. Les règles qui n'ont pas pu être évaluées ne sont pas ici mais
    /// dans la section des limites, où l'on va chercher ce que l'outil n'a pas fait.
    /// </remarks>
    private static void Findings(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 12. Constats de l'examen").AppendLine();

        if (run.Audit is not { } audit)
        {
            builder.AppendLine("Sans objet : ce mode n'examine pas le fichier de production.").AppendLine();
            return;
        }

        if (audit.Findings.Count == 0)
        {
            builder.AppendLine(audit.IsComplete
                ? "Aucun constat, et toutes les règles ont pu être évaluées."
                : "Aucun constat — mais toutes les règles n'ont pas pu être évaluées : voir les limites.")
                .AppendLine();

            return;
        }

        foreach (IGrouping<string, Audit.Finding> group in audit.Findings
            .GroupBy(finding => finding.RuleId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.Append("### ").Append(group.Key).Append(" — ")
                .Append(group.Count().ToString(CultureInfo.InvariantCulture))
                .AppendLine(" constat(s)").AppendLine();

            foreach (Audit.Finding finding in group)
            {
                Finding(builder, finding);
            }

            builder.AppendLine();
        }
    }

    private static void Finding(StringBuilder builder, Audit.Finding finding)
    {
        builder.Append("- **").Append(Severity(finding.Severity)).Append("** · ")
            .Append(Safety(finding.Safety)).Append(" — ").AppendLine(finding.Message);

        builder.Append("  - emplacement : `").Append(finding.Location.Json).Append("` (")
            .Append(finding.Location.Display).AppendLine(")");

        if (finding.Locations.Count > 1)
        {
            builder.Append("  - ")
                .Append((finding.Locations.Count - 1).ToString(CultureInfo.InvariantCulture))
                .AppendLine(" autre(s) emplacement(s) du même groupe");
        }

        if (finding.EstimatedBytesSaved is { } saved)
        {
            builder.Append("  - économie estimée : ")
                .Append(saved.ToString(CultureInfo.InvariantCulture)).AppendLine(" octets");
        }

        if (finding.SuggestedComponentName is { } name)
        {
            builder.Append("  - nom de composant suggéré : `").Append(name).AppendLine("`");
        }

        if (finding.Evidence?.Reasoning is { } reasoning)
        {
            builder.Append("  - déduction : ").AppendLine(reasoning);
        }

        builder.Append("  - à faire : ").AppendLine(finding.RecommendedAction);

        if (finding.AutofixAvailable)
        {
            builder.AppendLine("  - un remède automatique existe");
        }
    }

    /// <summary>
    /// Les groupes de répétition, mesurés, avec le seuil à partir duquel l'extraction gagne.
    /// </summary>
    /// <remarks>
    /// Le seuil est dans le tableau parce qu'il est la moitié de l'information : un groupe de deux
    /// occurrences au-dessous de son seuil n'est pas une occasion manquée, c'est une extraction qui
    /// coûterait plus qu'elle ne rapporte, et le tableau doit permettre de le voir sans calculer.
    /// </remarks>
    private static void Redundancy(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 13. Groupes de redondance").AppendLine();

        IReadOnlyList<Optimization.RedundancyGroup> groups = run.Plan?.Groups ?? [];

        if (groups.Count == 0)
        {
            builder.AppendLine("Aucun.").AppendLine();
            return;
        }

        builder.AppendLine(
            "| Règle | Niveau | Occurrences | Taille actuelle | Taille projetée | Économie estimée "
            + "| Seuil | Sûreté | Emplacement |");
        builder.AppendLine(
            "|-------|--------|-------------|-----------------|-----------------|------------------"
            + "|-------|--------|-------------|");

        foreach (Optimization.RedundancyGroup group in groups)
        {
            builder.Append("| ").Append(group.RuleId)
                .Append(" | ").Append(Tier(group.Tier))
                .Append(" | ").Append(Number(group.Count))
                .Append(" | ").Append(Number(group.Savings.CurrentBytes))
                .Append(" | ").Append(Number(group.Savings.ProjectedBytes))
                .Append(" | ").Append(Economy(group.Savings))
                .Append(" | ").Append(group.Savings.BreakEvenOccurrences is { } breakEven
                    ? Number(breakEven)
                    : "jamais")
                .Append(" | ").Append(Safety(group.Safety))
                .Append(" | `").Append(group.Location.Json)
                .AppendLine("` |");
        }

        builder.AppendLine();
    }

    /// <summary>Les sections 14 à 16 : ce qui est proposé, ce qui demande une relecture, et le total.</summary>
    private static void Proposals(StringBuilder builder, ApiDeltaRun run)
    {
        Optimization.OptimizationPlan plan = run.Plan ?? Optimization.OptimizationPlan.Empty;

        Suggestions(
            builder,
            "## 14. Propositions sûres",
            [.. plan.Safe],
            "Aucune : rien de ce qui a été trouvé ne se corrige sans relecture.");

        Suggestions(
            builder,
            "## 15. Propositions à revoir",
            [.. plan.ReviewRequired],
            "Aucune.");

        Reduction(builder, run, plan);
    }

    /// <summary>Une liste de propositions, chacune disant ce qu'elle remplace et ce qu'elle rapporte.</summary>
    private static void Suggestions(
        StringBuilder builder,
        string title,
        IReadOnlyList<Optimization.Transformation> proposals,
        string empty)
    {
        builder.AppendLine(title).AppendLine();

        if (proposals.Count == 0)
        {
            builder.AppendLine(empty).AppendLine();
            return;
        }

        foreach (Optimization.Transformation proposal in proposals
            .OrderByDescending(proposal => proposal.EstimatedBytesSaved)
            .ThenBy(proposal => proposal.RuleId, StringComparer.Ordinal))
        {
            Suggestion(builder, proposal);
        }

        builder.AppendLine();
    }

    private static void Suggestion(StringBuilder builder, Optimization.Transformation proposal)
    {
        builder.Append("- **").Append(proposal.RuleId).Append("** — ")
            .AppendLine(proposal.Summary);

        builder.Append("  - remplace ").Append(Number(proposal.Origins.Count))
            .Append(" écriture(s), à partir de `").Append(proposal.Origins[0].Json)
            .AppendLine("`");

        if (proposal.Destination is { } destination)
        {
            builder.Append("  - crée `").Append(destination.Json).AppendLine("`");
        }

        builder.Append("  - économie estimée : ").Append(Number(proposal.EstimatedBytesSaved))
            .Append(" octets");

        if (proposal.BreakEvenOccurrences is { } breakEven)
        {
            builder.Append(" (rentable à partir de ").Append(Number(breakEven))
                .Append(" occurrences)");
        }

        builder.AppendLine();

        if (proposal.ReviewReason is { } reason)
        {
            builder.Append("  - à relire : ").AppendLine(reason);
        }

        builder.Append("  - pour la défaire : ").AppendLine(proposal.Reversal.Text);
    }

    /// <summary>
    /// La section 16 : ce que le fichier pèse, ce qu'il pèserait, et ce que l'outil refuse de publier.
    /// </summary>
    private static void Reduction(
        StringBuilder builder,
        ApiDeltaRun run,
        Optimization.OptimizationPlan plan)
    {
        builder.AppendLine("## 16. Réduction de taille estimée").AppendLine();

        builder.Append("- Taille actuelle : ").Append(Number(run.Production.ByteLength))
            .AppendLine(" octets");
        builder.Append("- Économie estimée : ").Append(Number(plan.TotalEstimatedSaving))
            .AppendLine(" octets");
        builder.Append("- Taille projetée : ").Append(Number(plan.Growth.ProjectedBytes))
            .AppendLine(" octets");

        if (plan.Growth.Exceeded)
        {
            builder.AppendLine(
                "- **La sortie dépasserait la croissance tolérée** : rien ne sera publié.");
        }

        Applied(builder, run);

        int uncovered = plan.Uncovered.Count();

        if (uncovered > 0)
        {
            builder.Append("- ").Append(Number(uncovered))
                .AppendLine(
                    " répétition(s) mesurée(s) ne sont couvertes par aucune proposition : soit "
                    + "l'extraction coûterait plus qu'elle ne rapporte, soit l'outil ne sait pas "
                    + "établir que le remède laisse les appelants inchangés.");
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Ce qui a été appliqué, et ce que la vérification en a dit.
    /// </summary>
    /// <remarks>
    /// Un échec de vérification est dit ici, dans la section qui parle de la réduction, parce que c'est là
    /// qu'un lecteur cherchera ce que le nettoyage a donné. Le reléguer aux limites aurait laissé la
    /// section annoncer une économie que rien n'a produite.
    /// </remarks>
    private static void Applied(StringBuilder builder, ApiDeltaRun run)
    {
        if (run.Optimization is not { } optimization)
        {
            return;
        }

        builder.Append("- Transformations appliquées : ")
            .AppendLine(Number(optimization.Applied.Count));

        if (optimization.Declined.Count > 0)
        {
            builder.Append("- Écartées à l'application : ")
                .AppendLine(Number(optimization.Declined.Count));

            foreach (Optimization.DeclinedTransformation declined in optimization.Declined)
            {
                builder.Append("  - ").Append(declined.Transformation.RuleId).Append(" — ")
                    .AppendLine(declined.Reason);
            }
        }

        builder.Append(optimization.Verification.Established
                ? "- **Vérifiée** : "
                : "- **Rien n'a été publié** : ")
            .AppendLine(optimization.Verification.Explanation);
    }

    private static string Economy(Optimization.SavingsEstimate savings) =>
        Number(savings.AbsoluteSaving) + " (" + savings.PercentSaving.ToString(
            "F1", CultureInfo.InvariantCulture) + " %)";

    private static string Tier(Optimization.DuplicateTier tier) =>
        tier == Optimization.DuplicateTier.Exact ? "identique" : "voisin";

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Les constats tus, avec leur justification et leur propriétaire, et ce qu'il faut relire.
    /// </summary>
    /// <remarks>
    /// Une sourdine soustrait un constat au seuil d'échec, non au lecteur. C'est la seule façon de rendre
    /// un fichier de sourdines vérifiable : sans cette section, personne ne pourrait dire, en lisant un
    /// rapport, ce que l'équipe a choisi de ne pas voir.
    /// </remarks>
    private static void Suppressed(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 17. Constats mis en sourdine").AppendLine();

        Configuration.SuppressionOutcome outcome =
            run.Audit?.Suppressions ?? Configuration.SuppressionOutcome.None;

        if (!outcome.HasAnythingToSay)
        {
            builder.AppendLine("Aucun : cette exécution n'emploie aucune mise en sourdine.")
                .AppendLine();

            return;
        }

        if (outcome.Suppressed.Count > 0)
        {
            builder.AppendLine("| Règle | Emplacement | Justification | Propriétaire | Échéance |");
            builder.AppendLine("|-------|-------------|---------------|--------------|----------|");

            foreach (Configuration.SuppressedFinding silenced in outcome.Suppressed)
            {
                builder.Append("| ").Append(silenced.Finding.RuleId)
                    .Append(" | `").Append(silenced.Finding.Location.Json)
                    .Append("` | ").Append(silenced.Suppression.Justification ?? "—")
                    .Append(" | ").Append(silenced.Suppression.Owner ?? "—")
                    .Append(" | ").Append(silenced.Suppression.Expires is { } expires
                        ? expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : "sans")
                    .AppendLine(" |");
            }

            builder.AppendLine();
        }

        Overdue(builder, outcome);
    }

    /// <summary>Les sourdines échues et celles qui n'apparient plus rien.</summary>
    private static void Overdue(StringBuilder builder, Configuration.SuppressionOutcome outcome)
    {
        foreach (Configuration.Suppression expired in outcome.Expired)
        {
            builder.Append("- **Échue, donc non honorée** : ").Append(expired.RuleId)
                .Append(" en `").Append(expired.Location).Append("`, échéance ")
                .Append(expired.Expires?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(" — à relancer auprès de ").AppendLine(expired.Owner ?? "personne");
        }

        foreach (Configuration.Suppression unmatched in outcome.Unmatched)
        {
            builder.Append("- **Sans objet** : ").Append(unmatched.RuleId)
                .Append(" en `").Append(unmatched.Location)
                .AppendLine("` n'apparie plus aucun constat — l'emplacement a sans doute été corrigé "
                    + "ou renommé.");
        }

        if (outcome.Expired.Count > 0 || outcome.Unmatched.Count > 0)
        {
            builder.AppendLine();
        }
    }

    private static string Severity(Audit.FindingSeverity severity) => severity switch
    {
        Audit.FindingSeverity.Error => "erreur",
        Audit.FindingSeverity.Warning => "avertissement",
        Audit.FindingSeverity.Information => "information",
        _ => "occasion",
    };

    private static string Safety(Audit.SafetyClass safety) => safety switch
    {
        Audit.SafetyClass.Safe => "sûr",
        Audit.SafetyClass.ReviewRequired => "à relire",
        _ => "signalé seulement",
    };

    private static void Artifacts(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 18. Artefacts produits").AppendLine();
        builder.AppendLine("| Étape | Issue |");
        builder.AppendLine("|-------|-------|");

        foreach (RunStepState step in run.Steps)
        {
            builder.Append("| ").Append(step.Name).Append(" | ").Append(step.Status);

            if (step.SkipReason is { } reason)
            {
                builder.Append(" — ").Append(reason);
            }

            builder.AppendLine(" |");
        }

        builder.AppendLine();
    }


    /// <summary>
    /// Ce qui est entré en production, et ce qui a été écarté.
    /// </summary>
    /// <remarks>
    /// Présente même quand rien n'a été écrit. Un développeur qui a retenu trois éléments et n'en trouve
    /// aucun dans le document doit lire ici pourquoi, au même endroit qu'il aurait lu le succès : une
    /// section qui disparaîtrait quand elle a quelque chose de désagréable à dire serait la pire des
    /// sections.
    /// </remarks>
    private static void Carried(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 20. Ce qui est entré en production").AppendLine();

        if (run.Correction is not { } correction)
        {
            builder.AppendLine("Sans objet : aucune production consolidée n'a été demandée.").AppendLine();

            return;
        }

        builder.AppendLine(correction.Written
            ? "La production consolidée a été écrite dans « " + ArtifactNames.ConsolidatedDocument
                + " ». Votre fichier de production n'a pas été touché."
            : "Aucune production consolidée n'a été écrite.").AppendLine();

        Terminator(builder, run);

        builder.AppendLine("| Ce qui a été retenu | Effet |");
        builder.AppendLine("|---------------------|-------|");

        foreach (Operations.OperationSelector route in correction.Plan.Routes)
        {
            builder.Append("| ").Append(route.Display).AppendLine(" | route neuve |");
        }

        foreach (Correction.PlannedDifference difference in correction.Plan.Differences)
        {
            builder.Append("| ").Append(difference.Display).Append(" | ")
                .Append(Name(difference.Impact)).AppendLine(" |");
        }

        if (correction.Plan.Count == 0)
        {
            builder.AppendLine("| — | rien n'a pu être reporté |");
        }

        builder.AppendLine();

        Derivations(builder, run, correction);
        Declined(builder, correction);

        if (!correction.Verification.Established && correction.Plan.Count > 0)
        {
            builder.Append("**Rien n'a été publié.** ")
                .AppendLine(correction.Verification.Explanation).AppendLine();
        }
    }

    /// <summary>
    /// D'où vient chaque route proposée depuis un contrat de service, et ce qui y reste non rempli.
    /// </summary>
    /// <remarks>
    /// La dérivation est écrite, et non laissée à déduire. Un développeur qui lit « POST /documents »
    /// sans savoir que le chemin vient du nom de l'opération et la méthode du verbe « deposer » ne peut
    /// que l'accepter ou la refuser ; en voyant la dérivation, il peut la corriger — et c'est tout
    /// l'objet de la correspondance.
    /// <para>
    /// Le repli est annoncé comme un repli. Un chemin qu'aucun verbe n'a produit et qui serait présenté
    /// comme « dérivé » ferait passer plus vite sur une proposition qu'il faut justement regarder.
    /// </para>
    /// </remarks>
    private static void Derivations(
        StringBuilder builder, ApiDeltaRun run, Correction.CorrectionOutcome correction)
    {
        if (run.Footing is not { } footing || footing.Derivation.IsEmpty)
        {
            return;
        }

        builder.AppendLine("### Dérivation des routes proposées").AppendLine();
        builder.AppendLine("| Opération du contrat | Itinéraire | Comment |");
        builder.AppendLine("|----------------------|------------|---------|");

        foreach (KeyValuePair<string, Projection.DerivedRoute> derived in footing.Derivation.Derivations)
        {
            builder.Append("| ").Append(derived.Key).Append(" | ").Append(derived.Value.Display)
                .Append(" | ").Append(How(derived.Value)).AppendLine(" |");
        }

        builder.AppendLine();

        Unfilled(builder, correction.Plan);
        Collisions(builder, correction.Plan);
    }

    /// <summary>Comment un itinéraire a été obtenu, en une formule qui ne promet pas plus qu'elle ne tient.</summary>
    private static string How(Projection.DerivedRoute derived) => derived.Kind switch
    {
        Projection.RouteDerivationKind.Derived =>
            "dérivé : méthode d'après le verbe « " + derived.Verb + " », chemin d'après le sujet",
        Projection.RouteDerivationKind.Partial =>
            "partiel : chemin d'après le sujet, aucun verbe reconnu — la méthode est « POST » par défaut",
        _ => "repli : rien de reconnaissable dans le nom, l'itinéraire n'est pas une dérivation",
    };

    /// <summary>Ce qui reste non rempli dans le document engendré, et combien.</summary>
    private static void Unfilled(StringBuilder builder, Correction.CorrectionPlan plan)
    {
        if (plan.Unfilled.Count == 0)
        {
            return;
        }

        builder.Append("**Endroits non remplis : ")
            .Append(plan.Unfilled.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(".** La projection n'y a rien porté, et l'outil refuse de les deviner. Le "
                + "document écrit ne le dit pas : il est destiné à devenir un contrat publié et ne "
                + "porte que de l'OpenAPI. C'est donc ici, et ici seulement, que chacun est nommé.")
            .AppendLine();

        foreach (Correction.UnfilledMarker marker in plan.Unfilled)
        {
            builder.Append("- ").Append(marker.Target.Display).Append(" — ")
                .AppendLine(marker.Display);
        }

        builder.AppendLine();
    }

    /// <summary>Les chemins dérivés qui tombent sur un chemin que la production porte déjà.</summary>
    private static void Collisions(StringBuilder builder, Correction.CorrectionPlan plan)
    {
        if (plan.Collisions.Count == 0)
        {
            return;
        }

        builder.AppendLine("**Chemins déjà portés par la production.**").AppendLine();

        foreach (Correction.PathCollision collision in plan.Collisions)
        {
            builder.Append("- ").AppendLine(collision.Display);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Prévient quand la production ne finissait pas par une fin de ligne.
    /// </summary>
    /// <remarks>
    /// Une phrase qui a coûté une matinée à quelqu'un. Un fichier dont la dernière ligne n'a pas de fin
    /// de ligne en gagne une dès qu'on écrit après elle, et <em>tout</em> outil de comparaison affiche
    /// alors cette ligne comme modifiée. Son contenu ne l'est pas — le vérificateur l'établit à l'octet
    /// avant que le fichier n'existe — mais le relecteur lit « ligne supprimée » et cherche ce qu'il a
    /// perdu.
    /// <para>
    /// Rien à corriger dans l'écriture : écrire après la dernière ligne la termine, et placer la
    /// déclaration ailleurs serait arbitraire. Ce qui manquait, c'était de le dire.
    /// </para>
    /// </remarks>
    private static void Terminator(StringBuilder builder, ApiDeltaRun run)
    {
        ReadOnlySpan<byte> bytes = run.Production.Source.Span;

        if (bytes.Length == 0 || bytes[^1] == (byte)'\n')
        {
            return;
        }

        builder.AppendLine(
            "**Votre fichier de production ne finissait pas par une fin de ligne.** La consolidation "
            + "écrit après sa dernière ligne, qui en gagne donc une. Un outil de comparaison affichera "
            + "cette ligne comme modifiée : son contenu ne l'est pas, et la vérification l'a établi à "
            + "l'octet avant que le fichier ne soit écrit.").AppendLine();
    }

    /// <summary>
    /// L'appariement entre les opérations du contrat et les routes de la production.
    /// </summary>
    /// <remarks>
    /// Présente même vide, comme les deux suivantes. Une section qui disparaît quand elle n'a rien à
    /// dire laisse un lecteur incapable de distinguer le silence de l'absence : il ne sait pas si
    /// l'outil n'a rien trouvé ou s'il n'a pas regardé.
    /// </remarks>
    private static void Correspondence(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 21. Appariement des opérations").AppendLine();

        if (run.Footing is not { } footing || footing.Correspondence.Entries.Count == 0)
        {
            builder.AppendLine(Absent(run)).AppendLine();

            return;
        }

        builder.AppendLine(footing.Correspondence.IsConfirmed
            ? "L'appariement a été confirmé. Rien n'a été comparé avant cette confirmation."
            : "**L'appariement n'a pas été confirmé**, et rien n'a donc été comparé.").AppendLine();

        builder.AppendLine("| Opération | Route | Origine | Sur quoi |");
        builder.AppendLine("|-----------|-------|---------|----------|");

        foreach (Correspondence.CorrespondenceEntry entry in footing.Correspondence.Entries)
        {
            builder.Append("| ").Append(entry.Operation)
                .Append(" | ").Append(entry.HasRoute ? entry.Route : "—")
                .Append(" | ").Append(entry.Mark)
                .Append(" | ").Append(entry.Basis ?? "—").AppendLine(" |");
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Ce que la production couvre du contrat, et ce qu'elle ne couvre pas.
    /// </summary>
    /// <remarks>
    /// Séparée de la compatibilité, et c'est tout le sujet. La couverture dit ce qui manque, avec
    /// certitude, parce qu'elle repose sur l'appariement confirmé et sur rien d'autre. Les mêler ferait
    /// apparaître un manque parmi les ruptures : rien n'est cassé quand une opération n'a pas de route,
    /// et les deux nouvelles ne s'adressent ni au même lecteur ni au même calendrier.
    /// </remarks>
    private static void Coverage(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 22. Couverture du contrat").AppendLine();

        if (run.Footing is not { } footing || !footing.Coverage.IsEstablished)
        {
            builder.AppendLine(run.Footing is null
                ? Absent(run)
                : "La couverture n'a pas été établie : l'appariement n'a pas été confirmé. Ce n'est "
                    + "pas une couverture totale, et ce n'est pas une couverture nulle — c'est un "
                    + "constat qui n'a pas eu lieu.").AppendLine();

            return;
        }

        builder.AppendLine("| État | Opérations |");
        builder.AppendLine("|------|------------|");
        builder.Append("| couvertes | ").Append(Count(footing.Coverage.Covered)).AppendLine(" |");
        builder.Append("| absentes de la production | ").Append(Count(footing.Coverage.Gaps))
            .AppendLine(" |");
        builder.Append("| ambiguës | ").Append(Count(footing.Coverage.Ambiguous)).AppendLine(" |");
        builder.AppendLine();

        foreach (Correspondence.CoverageEntry entry in footing.Coverage.Entries
            .Where(entry => entry.Remedy is not null))
        {
            builder.Append("- **").Append(entry.Operation).Append("** — ").AppendLine(entry.Remedy);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Quelle part du contrat avait une contrepartie comparable, en chiffres.
    /// </summary>
    /// <remarks>
    /// Un chiffre, et non une tournure. « La projection s'est bien passée » laisse croire à une
    /// équivalence ; « 62 % des constructions portées » se recoupe avec le reste du rapport et se
    /// discute. Un lecteur doit pouvoir distinguer un rapport propre d'un rapport muet.
    /// </remarks>
    private static void Comparability(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 23. Part comparable du contrat").AppendLine();

        if (run.Footing?.Projection is not { } projection)
        {
            builder.AppendLine(Absent(run)).AppendLine();

            return;
        }

        Projection.ProjectionFigures figures = projection.Figures;
        int examined = figures.Carried + figures.Approximate + figures.NotCarried;

        builder.AppendLine("| Ce qui est compté | Nombre |");
        builder.AppendLine("|-------------------|--------|");
        builder.Append("| opérations projetées | ").Append(Figure(figures.Operations)).AppendLine(" |");
        builder.Append("| opérations appariées | ")
            .Append(Figure(run.Footing.Correspondence.Comparable.Count)).AppendLine(" |");
        builder.Append("| constructions portées | ").Append(Figure(figures.Carried)).AppendLine(" |");
        builder.Append("| portées à peu près | ").Append(Figure(figures.Approximate)).AppendLine(" |");
        builder.Append("| non portées | ").Append(Figure(figures.NotCarried)).AppendLine(" |");
        builder.Append("| types déclarés non projetables | ").Append(Figure(figures.DeclinedTypes))
            .AppendLine(" |");
        builder.AppendLine();

        builder.Append("**Part portée exactement : ").Append(Percent(figures.Carried, examined))
            .Append(" des ").Append(Figure(examined))
            .AppendLine(" constructions examinées.**").AppendLine();

        Grouped(builder, projection);
    }

    /// <summary>Ce qui n'a pas été porté, groupé par motif et compté.</summary>
    /// <remarks>
    /// Une entrée par motif, et son nombre. Quatre cents attributs non portés enseignent la même chose
    /// au prix de quatre cents lignes ; le rapport en montre une, et chaque occurrence garde son
    /// emplacement dans le document engendré.
    /// </remarks>
    private static void Grouped(StringBuilder builder, Projection.ProjectionResult projection)
    {
        if (projection.ByReason.Count == 0)
        {
            builder.AppendLine("Rien n'a été laissé derrière : le contrat se projette en entier.")
                .AppendLine();

            return;
        }

        builder.AppendLine("| Ce qui n'a pas de contrepartie | Occurrences |");
        builder.AppendLine("|--------------------------------|-------------|");

        foreach (KeyValuePair<Projection.ProjectionGapReason, int> group in projection.ByReason
            .OrderByDescending(group => group.Value)
            .ThenBy(group => group.Key))
        {
            builder.Append("| ").Append(Projection.GapReasons.Say(group.Key))
                .Append(" | ").Append(Figure(group.Value)).AppendLine(" |");
        }

        builder.AppendLine();
    }

    /// <summary>Ce qu'une section dit quand le candidat n'est pas un contrat de service.</summary>
    private static string Absent(ApiDeltaRun run) => run.Candidate is null
        ? "Sans objet : cette exécution n'avait pas de candidat."
        : "Sans objet : le candidat n'est pas un contrat de service.";

    private static string Count<T>(IEnumerable<T> items) =>
        Figure(items.Count());

    private static string Figure(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Une proportion, ou l'aveu qu'il n'y a rien à proportionner.</summary>
    private static string Percent(int part, int total) => total == 0
        ? "sans objet"
        : ((part * 100) / total).ToString(CultureInfo.InvariantCulture) + " %";

    /// <summary>Ce qui a été retenu et écarté, avec la raison de chaque refus.</summary>
    private static void Declined(StringBuilder builder, Correction.CorrectionOutcome correction)
    {
        if (correction.Plan.Declined.Count == 0)
        {
            return;
        }

        builder.AppendLine("### Écarté").AppendLine();

        foreach (Correction.DeclinedCorrection declined in correction.Plan.Declined)
        {
            builder.Append("- **").Append(declined.What).Append("** — ").AppendLine(declined.Why);
        }

        builder.AppendLine();
    }

    private static void Limitations(StringBuilder builder, ApiDeltaRun run)
    {
        builder.AppendLine("## 19. Limites et hypothèses").AppendLine();

        builder.AppendLine(
            "- Une sortie réengendrée ne conserve ni les commentaires, ni les ancres, ni l'ordre "
            + "d'origine des clés : le fichier source est le seul endroit où ils survivent.");

        builder.AppendLine(
            "- Les références externes ne sont pas suivies : elles sont préservées telles quelles ou "
            + "refusées, selon la politique en vigueur.");

        foreach (RunStepState skipped in run.Steps.Where(step => step.SkipReason is not null))
        {
            builder.Append("- Étape non exécutée : ").Append(skipped.Name)
                .Append(" — ").AppendLine(skipped.SkipReason);
        }

        Unread(builder, run);
        Unevaluated(builder, run);

        builder.AppendLine();
    }

    /// <summary>
    /// Ce que la lecture du contrat n'a pas résolu, et ce que la projection n'a pas pu établir.
    /// </summary>
    /// <remarks>
    /// Dans les limites, et non dans une section à part : un import que la politique a refusé et un
    /// type déclaré non projetable ne sont pas des constats sur le contrat, ce sont des trous dans ce
    /// que le rapport affirme. Un lecteur doit les voir au même endroit que les autres réserves, avant
    /// de se fier au reste.
    /// </remarks>
    private static void Unread(StringBuilder builder, ApiDeltaRun run)
    {
        foreach (Diagnostic unresolved in run.Diagnostics.Where(IsImportGap))
        {
            builder.Append("- Import non résolu : ").AppendLine(unresolved.Message);
        }

        if (run.Footing?.Projection is not { } projection)
        {
            return;
        }

        if (projection.Figures.DeclinedTypes > 0)
        {
            builder.Append("- ").Append(Figure(projection.Figures.DeclinedTypes))
                .AppendLine(
                    " type(s) déclarés non projetables : leur sens dépend d'une construction que "
                    + "l'outil ne sait pas exprimer, et les projeter sans elle produirait un contrat "
                    + "plus permissif que le vrai.");
        }

        if (projection.Figures.NotCarried > 0)
        {
            builder.Append("- ").Append(Figure(projection.Figures.NotCarried))
                .AppendLine(
                    " construction(s) sans contrepartie : la section 23 les groupe par motif, et le "
                    + "document engendré porte un marqueur à chaque endroit concerné.");
        }
    }

    /// <summary>Indique qu'un diagnostic dit qu'un import n'a pas été suivi.</summary>
    private static bool IsImportGap(Diagnostic diagnostic) =>
        diagnostic.Code is Wsdl.WsdlDiagnosticCodes.ImportUnresolved
            or Wsdl.WsdlDiagnosticCodes.ImportOutsideRoot
            or Wsdl.WsdlDiagnosticCodes.ImportRemoteRefused
            or Wsdl.WsdlDiagnosticCodes.ImportCycle;

    /// <summary>
    /// Les règles qui n'ont pas pu être évaluées, et celles qui ont rendu moins qu'elles n'ont trouvé.
    /// </summary>
    /// <remarks>
    /// Dans les limites, et nommées. Un examen dont une règle n'a pas tourné n'est pas un examen propre,
    /// et le dire ici est la seule façon d'empêcher le rapport de se présenter comme complet.
    /// </remarks>
    private static void Unevaluated(StringBuilder builder, ApiDeltaRun run)
    {
        if (run.Audit is not { } audit)
        {
            return;
        }

        foreach (Audit.RuleOutcome outcome in audit.NotEvaluated.Concat(audit.Truncated)
            .OrderBy(outcome => outcome.RuleId, StringComparer.Ordinal))
        {
            builder.Append("- Règle ").Append(outcome.RuleId).Append(" : ")
                .Append(outcome.State == Audit.RuleState.NotEvaluated
                    ? "non évaluée"
                    : "rendue incomplète")
                .Append(" — ").AppendLine(outcome.Reason ?? "sans raison consignée");
        }

        if (!audit.IsComplete)
        {
            builder.AppendLine(
                "- **Cet examen n'est pas complet** : les règles ci-dessus n'ont pas tourné, et "
                + "l'absence de constat de leur part ne veut donc pas dire qu'il n'y a rien.");
        }
    }

    private static void Section(
        StringBuilder builder,
        string title,
        ApiDeltaRun run,
        Func<Difference, bool> predicate)
    {
        builder.Append("## ").AppendLine(title).AppendLine();

        IReadOnlyList<Difference> selected = [.. run.Differences.Where(predicate)];

        if (selected.Count == 0)
        {
            builder.AppendLine("Aucun.").AppendLine();
            return;
        }

        foreach (Difference difference in selected)
        {
            builder.Append("- `").Append(difference.ChangeId).Append("` ")
                .Append(difference.Location.Display).AppendLine();

            builder.Append("  - emplacement : `").Append(difference.Location.Json).AppendLine("`");
            builder.Append("  - effet : ").Append(Name(difference.Impact))
                .Append(" (assurance : ").Append(difference.Confidence).AppendLine(")");

            if (difference.Before is { } before)
            {
                builder.Append("  - avant : ").AppendLine(before.Text);
            }

            if (difference.After is { } after)
            {
                builder.Append("  - après : ").AppendLine(after.Text);
            }

            builder.Append("  - ").AppendLine(difference.Rationale);
            builder.Append("  - à faire : ").AppendLine(difference.RecommendedAction);
        }

        builder.AppendLine();
    }

    private static bool IsOperation(Difference difference)
    {
        string[] segments = difference.Location.Json.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 3
            && string.Equals(segments[0], "paths", StringComparison.Ordinal);
    }

    private static bool IsUnderPaths(Difference difference) =>
        difference.Location.Json.StartsWith("/paths", StringComparison.Ordinal);

    private static string Name(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking => "cassant",
        ChangeImpact.NonBreaking => "compatible",
        ChangeImpact.DocumentationOnly => "documentation",
        _ => "inconnu",
    };
}
