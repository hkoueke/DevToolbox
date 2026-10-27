using System.Text.Json;
using System.Text.Json.Nodes;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Loading;

namespace DevToolbox.Tools.ApiDelta.Core.Reporting;

/// <summary>
/// Écrit le rapport lisible par une machine.
/// </summary>
/// <remarks>
/// La disposition des champs est un contrat : un champ présent ici n'est jamais renommé ni détourné, et
/// <c>schemaVersion</c> dit à un consommateur ce qu'il peut attendre. Les champs à venir — l'audit, le
/// plan, l'extrait — s'ajouteront sans déplacer ceux-ci.
/// <para>
/// Construit en <c>JsonNode</c> plutôt que par sérialisation de types annotés : la sortie doit être une
/// forme stable que l'on relit en la regardant, non le reflet d'une hiérarchie de classes qui évoluerait
/// avec le code.
/// </para>
/// </remarks>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    /// <summary>La version de la disposition des champs.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Écrit le rapport.</summary>
    /// <param name="run">L'exécution achevée.</param>
    /// <returns>Le rapport, en octets.</returns>
    public static ReadOnlyMemory<byte> Write(ApiDeltaRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        JsonObject report = new()
        {
            ["schemaVersion"] = SchemaVersion,
            ["runMetadata"] = Metadata(run),
            ["inputs"] = Inputs(run),
            ["diagnostics"] = Diagnostics(run),
            ["differences"] = Differences(run),
            ["version"] = Version(run),
            ["ruleOutcomes"] = Outcomes(run),
            ["findings"] = Findings(run),
            ["redundancyGroups"] = RedundancyGroups(run),
            ["extract"] = Extracted(run),
            ["suppressions"] = Suppressions(run),
            ["optimization"] = Planned(run),
            ["sizes"] = Sizes(run),
            ["correspondence"] = Correspondence(run),
            ["coverage"] = Coverage(run),
            ["comparability"] = Comparability(run),
            ["steps"] = Steps(run),
            ["outcome"] = Outcome(run),
        };

        return JsonSerializer.SerializeToUtf8Bytes(report, Options);
    }

    /// <summary>
    /// Le bloc où vivent l'heure, la durée et les chemins — et le seul.
    /// </summary>
    /// <remarks>
    /// Tout ce qui varie d'une exécution à l'autre est rassemblé ici, afin que le reste du rapport puisse
    /// être comparé octet pour octet d'une exécution à la suivante.
    /// </remarks>
    private static JsonObject Metadata(ApiDeltaRun run) => new()
    {
        ["runId"] = run.RunId.ToString(),
        ["startedAt"] = run.StartedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        ["durationMs"] = (long)run.Duration.TotalMilliseconds,
    };

    /// <summary>
    /// Le sort de chaque règle enregistrée, et non seulement de celles qui ont trouvé quelque chose.
    /// </summary>
    /// <remarks>
    /// C'est ce qui permet à une chaîne d'intégration de distinguer « rien à signaler » de « la règle n'a
    /// pas tourné ». Un tableau de constats vide ne porte pas cette différence, et c'est pourtant la seule
    /// qui décide si l'on peut publier.
    /// </remarks>
    private static JsonArray Outcomes(ApiDeltaRun run)
    {
        JsonArray outcomes = [];

        foreach (Audit.RuleOutcome outcome in run.Audit?.Outcomes ?? [])
        {
            JsonObject entry = new()
            {
                ["ruleId"] = outcome.RuleId,
                ["state"] = outcome.State.ToString().ToLowerInvariant(),
                ["findingCount"] = outcome.FindingCount,
            };

            if (outcome.Reason is { } reason)
            {
                entry["reason"] = reason;
            }

            outcomes.Add(entry);
        }

        return outcomes;
    }

    private static JsonArray Findings(ApiDeltaRun run)
    {
        JsonArray findings = [];

        foreach (Audit.Finding finding in run.Audit?.Findings ?? [])
        {
            findings.Add(Entry(finding));
        }

        return findings;
    }

    private static JsonObject Entry(Audit.Finding finding)
    {
        JsonArray locations = [];

        foreach (Syntax.SpecPointer location in finding.Locations)
        {
            locations.Add(location.Json);
        }

        JsonObject entry = new()
        {
            ["ruleId"] = finding.RuleId,
            ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
            ["safety"] = Safety(finding.Safety),
            ["message"] = finding.Message,
            ["jsonPointer"] = finding.Location.Json,
            ["displayPath"] = finding.Location.Display,
            ["locations"] = locations,
            ["suggestedComponentName"] = finding.SuggestedComponentName,
            ["estimatedBytesSaved"] = finding.EstimatedBytesSaved,
            ["autofixAvailable"] = finding.AutofixAvailable,
            ["recommendedAction"] = finding.RecommendedAction,
        };

        if (finding.Evidence is { } evidence)
        {
            entry["evidence"] = Grounds(evidence);
        }

        return entry;
    }

    private static JsonObject Grounds(Audit.FindingEvidence evidence)
    {
        JsonArray differing = [];

        foreach (string field in evidence.DifferingFields)
        {
            differing.Add(field);
        }

        return new JsonObject
        {
            ["occurrences"] = evidence.Occurrences,
            ["fingerprint"] = evidence.Fingerprint is { } fingerprint ? "sha256:" + fingerprint : null,
            ["profile"] = evidence.Profile,
            ["differingFields"] = differing,
            ["reasoning"] = evidence.Reasoning,
        };
    }

    /// <summary>Les groupes de répétition, mesurés et chiffrés.</summary>
    private static JsonArray RedundancyGroups(ApiDeltaRun run)
    {
        JsonArray groups = [];

        foreach (Optimization.RedundancyGroup group in run.Plan?.Groups ?? [])
        {
            groups.Add(new JsonObject
            {
                ["ruleId"] = group.RuleId,
                ["fingerprint"] = "sha256:" + group.Fingerprint,
                ["profile"] = group.Profile,
                ["kind"] = Optimization.CandidateKindNames.Of(group.Kind),
                ["tier"] = group.Tier.ToString().ToLowerInvariant(),
                ["occurrences"] = group.Count,
                ["currentBytes"] = group.Savings.CurrentBytes,
                ["projectedBytes"] = group.Savings.ProjectedBytes,
                ["absoluteSaving"] = group.Savings.AbsoluteSaving,
                ["percentSaving"] = group.Savings.PercentSaving,
                ["breakEvenOccurrences"] = group.Savings.BreakEvenOccurrences,
                ["recommended"] = group.Savings.Recommended,
                ["safety"] = Safety(group.Safety),
                ["jsonPointer"] = group.Location.Json,
            });
        }

        return groups;
    }

    /// <summary>
    /// L'extrait : d'où il vient, ce que chaque opération a hérité, et ce que la vérification a établi.
    /// </summary>
    /// <remarks>
    /// L'héritage est publié opération par opération parce que c'est ce qu'un consommateur ne peut pas
    /// reconstituer depuis l'extrait seul : un paramètre remonté au niveau du chemin y est indiscernable
    /// d'un paramètre qui y était déjà.
    /// </remarks>
    /// <summary>
    /// L'appariement, entrée par entrée.
    /// </summary>
    /// <remarks>
    /// Présent et nul plutôt qu'absent, comme les deux suivants : un consommateur machine doit pouvoir
    /// distinguer « il n'y avait pas de contrat de service » de « la clé a changé de nom ». Le rapport
    /// lisible fait la même chose avec une phrase, pour la même raison.
    /// </remarks>
    private static JsonObject? Correspondence(ApiDeltaRun run)
    {
        if (run.Footing is not { } footing || footing.Correspondence.Entries.Count == 0)
        {
            return null;
        }

        JsonArray entries = [];

        foreach (Core.Correspondence.CorrespondenceEntry entry in footing.Correspondence.Entries)
        {
            entries.Add(new JsonObject
            {
                ["operation"] = entry.Operation,
                ["route"] = entry.HasRoute ? entry.Route : null,
                ["origin"] = entry.Origin.ToString(),
                ["inferred"] = entry.IsInferred,
                ["basis"] = entry.Basis,
                ["alternatives"] = Words(entry.Alternatives),
            });
        }

        return new JsonObject
        {
            ["confirmed"] = footing.Correspondence.IsConfirmed,
            ["entries"] = entries,
        };
    }

    /// <summary>Ce que la production couvre du contrat, opération par opération.</summary>
    private static JsonObject? Coverage(ApiDeltaRun run)
    {
        if (run.Footing is not { } footing || !footing.Coverage.IsEstablished)
        {
            return null;
        }

        JsonArray entries = [];

        foreach (Core.Correspondence.CoverageEntry entry in footing.Coverage.Entries)
        {
            entries.Add(new JsonObject
            {
                ["operation"] = entry.Operation,
                ["state"] = entry.State.ToString(),
                ["route"] = entry.Route,
                ["remedy"] = entry.Remedy,
            });
        }

        return new JsonObject
        {
            ["covered"] = footing.Coverage.Covered.Count(),
            ["absent"] = footing.Coverage.Gaps.Count(),
            ["ambiguous"] = footing.Coverage.Ambiguous.Count(),
            ["entries"] = entries,
        };
    }

    /// <summary>Quelle part du contrat avait une contrepartie, en chiffres recoupables.</summary>
    private static JsonObject? Comparability(ApiDeltaRun run)
    {
        if (run.Footing?.Projection is not { } projection)
        {
            return null;
        }

        Projection.ProjectionFigures figures = projection.Figures;
        int examined = figures.Carried + figures.Approximate + figures.NotCarried;
        JsonObject grouped = [];

        foreach (KeyValuePair<Projection.ProjectionGapReason, int> group in projection.ByReason
            .OrderBy(group => group.Key))
        {
            grouped[group.Key.ToString()] = group.Value;
        }

        return new JsonObject
        {
            ["operations"] = figures.Operations,
            ["pairedOperations"] = run.Footing.Correspondence.Comparable.Count,
            ["schemas"] = figures.Schemas,
            ["carried"] = figures.Carried,
            ["approximate"] = figures.Approximate,
            ["notCarried"] = figures.NotCarried,
            ["declinedTypes"] = figures.DeclinedTypes,
            ["examined"] = examined,

            // Le quotient est écrit plutôt que laissé à calculer : deux consommateurs qui l'arrondissent
            // autrement rapporteraient deux chiffres pour le même constat.
            ["carriedPercent"] = examined == 0 ? null : (figures.Carried * 100) / examined,
            ["notCarriedByReason"] = grouped,
        };
    }

    private static JsonObject? Extracted(ApiDeltaRun run)
    {
        if (run.Extract is not { } extract)
        {
            return null;
        }

        JsonArray operations = [];

        foreach (Extraction.InheritanceRecord record in extract.InheritedContext)
        {
            operations.Add(new JsonObject
            {
                ["path"] = record.Selector.Path,
                ["method"] = record.Selector.MethodKey,
                ["inherited"] = new JsonObject
                {
                    ["pathParameters"] = Words(record.PathParameters),
                    ["servers"] = Words(record.Servers),
                    ["security"] = Words(record.Security),
                },
            });
        }

        return new JsonObject
        {
            ["source"] = new JsonObject
            {
                ["path"] = extract.Provenance.SourcePath,
                ["sha256"] = extract.Provenance.SourceHash,
            },
            ["operations"] = operations,
            ["closure"] = new JsonObject
            {
                ["components"] = extract.Closure.Required.Count,
                ["externalPreserved"] = extract.Closure.ExternalPreserved.Count,
            },
            ["carriedTags"] = Words(extract.CarriedTags),
            ["danglingExtensionReferences"] = Dangling(extract),
            ["verification"] = new JsonObject
            {
                ["reparsed"] = extract.Verification.Reparsed,
                ["revalidated"] = extract.Verification.Revalidated,
                ["referencesResolved"] = extract.Verification.ReferencesResolved,
                ["containsNothingUnselected"] = extract.Verification.ContainsNothingUnselected,
                ["operationsEquivalent"] = extract.Verification.OperationsEquivalent,
                ["differences"] = Words(extract.Verification.Differences),
            },
        };
    }

    private static JsonArray Dangling(Extraction.ExtractResult extract)
    {
        JsonArray array = [];

        foreach (Audit.Finding finding in extract.DanglingExtensionReferences)
        {
            array.Add(Entry(finding));
        }

        return array;
    }

    private static JsonArray Words(IEnumerable<string> words)
    {
        JsonArray array = [];

        foreach (string word in words)
        {
            array.Add(word);
        }

        return array;
    }

    /// <summary>
    /// Les mises en sourdine, chacune avec son état.
    /// </summary>
    /// <remarks>
    /// Un seul tableau pour les trois états, parce qu'une chaîne d'intégration veut la même chose des
    /// trois : savoir ce que l'équipe a décidé de taire, et ce qui dans ces décisions a cessé de valoir.
    /// L'état dit laquelle des trois on lit.
    /// </remarks>
    private static JsonArray Suppressions(ApiDeltaRun run)
    {
        Configuration.SuppressionOutcome outcome =
            run.Audit?.Suppressions ?? Configuration.SuppressionOutcome.None;

        JsonArray array = [];

        foreach (Configuration.SuppressedFinding silenced in outcome.Suppressed)
        {
            array.Add(Silenced(silenced.Suppression, "applied", silenced.Finding));
        }

        foreach (Configuration.Suppression expired in outcome.Expired)
        {
            array.Add(Silenced(expired, "expired", null));
        }

        foreach (Configuration.Suppression unmatched in outcome.Unmatched)
        {
            array.Add(Silenced(unmatched, "unmatched", null));
        }

        return array;
    }

    private static JsonObject Silenced(
        Configuration.Suppression suppression,
        string state,
        Audit.Finding? finding) => new()
        {
            ["ruleId"] = suppression.RuleId,
            ["location"] = suppression.Location,
            ["state"] = state,
            ["justification"] = suppression.Justification,
            ["owner"] = suppression.Owner,
            ["expires"] = suppression.Expires?.ToString(
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["jsonPointer"] = finding?.Location.Json,
            ["message"] = finding?.Message,
        };

    /// <summary>
    /// Le plan : ce qui est proposé, ce que cela rapporterait, et ce que la croissance autorise.
    /// </summary>
    /// <remarks>
    /// L'état de chaque transformation est publié, et il vaut <c>proposed</c> tant que rien n'a été
    /// appliqué. Une chaîne d'intégration peut donc distinguer un devis d'un travail fait, ce qu'un
    /// tableau de transformations sans état ne permettrait pas.
    /// </remarks>
    private static JsonObject Planned(ApiDeltaRun run)
    {
        Optimization.OptimizationPlan plan =
            run.Plan ?? Optimization.OptimizationPlan.Empty;

        JsonArray transformations = [];

        foreach (Optimization.Transformation transformation in plan.Transformations)
        {
            transformations.Add(Proposal(transformation));
        }

        JsonObject node = new()
        {
            ["transformations"] = transformations,
            ["totalEstimatedSaving"] = plan.TotalEstimatedSaving,
            ["appliedCount"] = run.Optimization?.Applied.Count ?? plan.AppliedCount,
            ["growthCheck"] = new JsonObject
            {
                ["projectedBytes"] = plan.Growth.ProjectedBytes,
                ["limit"] = plan.Growth.Limit,
                ["exceeded"] = plan.Growth.Exceeded,
            },
        };

        if (run.Optimization is { } outcome)
        {
            node["applied"] = Applied(outcome);
            node["declined"] = Declined(outcome);
            node["verification"] = Verification(outcome.Verification);
        }

        return node;
    }

    /// <summary>Ce qui a été appliqué, dans l'état où cela a été laissé.</summary>
    private static JsonArray Applied(Optimization.OptimizationOutcome outcome)
    {
        JsonArray applied = [];

        foreach (Optimization.Transformation transformation in outcome.Applied)
        {
            applied.Add(Proposal(transformation));
        }

        return applied;
    }

    /// <summary>
    /// Ce qui a été écarté, avec sa raison.
    /// </summary>
    /// <remarks>
    /// Rendu, et non omis. Une transformation que le plan proposait et que l'application n'a pas faite
    /// est un fait dont une chaîne d'intégration a besoin : sans elle, un plan de quatre propositions et
    /// une copie qui n'en porte qu'une se présenteraient de la même façon.
    /// </remarks>
    private static JsonArray Declined(Optimization.OptimizationOutcome outcome)
    {
        JsonArray declined = [];

        foreach (Optimization.DeclinedTransformation entry in outcome.Declined)
        {
            JsonObject node = Proposal(entry.Transformation);
            node["declinedBecause"] = entry.Reason;

            declined.Add(node);
        }

        return declined;
    }

    private static JsonObject Verification(Optimization.VerificationResult verification)
    {
        JsonArray differences = [];

        foreach (string difference in verification.Differences)
        {
            differences.Add(difference);
        }

        return new JsonObject
        {
            ["reparsed"] = verification.Reparsed,
            ["revalidated"] = verification.Revalidated,
            ["referencesResolved"] = verification.ReferencesResolved,
            ["contractEquivalent"] = verification.ContractEquivalent,
            ["differences"] = differences,
        };
    }

    private static JsonObject Proposal(Optimization.Transformation transformation)
    {
        JsonArray origins = [];

        foreach (Syntax.SpecPointer origin in transformation.Origins)
        {
            origins.Add(origin.Json);
        }

        return new JsonObject
        {
            ["ruleId"] = transformation.RuleId,
            ["fromRuleId"] = transformation.FromRuleId,
            ["safety"] = Safety(transformation.Safety),
            ["state"] = transformation.State.ToString().ToLowerInvariant(),
            ["origins"] = origins,
            ["destination"] = transformation.Destination?.Json,
            ["evidence"] = transformation.Evidence is { } evidence ? "sha256:" + evidence : null,
            ["reversal"] = transformation.Reversal.Text,
            ["reversible"] = transformation.Reversal.Reversible,
            ["estimatedBytesSaved"] = transformation.EstimatedBytesSaved,
            ["breakEvenOccurrences"] = transformation.BreakEvenOccurrences,
            ["reviewReason"] = transformation.ReviewReason,
        };
    }

    /// <summary>
    /// Les tailles : celle du fichier lu, et celle que le plan projette.
    /// </summary>
    /// <remarks>
    /// <c>productionAfter</c> est une projection tant que rien n'a été appliqué, et non une mesure. Les
    /// deux portent le même nom dans le contrat de sortie parce qu'elles répondent à la même question ;
    /// <c>optimization.transformations[].state</c> dit laquelle des deux on lit.
    /// </remarks>
    private static JsonObject Sizes(ApiDeltaRun run)
    {
        Optimization.OptimizationPlan plan =
            run.Plan ?? Optimization.OptimizationPlan.Empty;

        return new JsonObject
        {
            ["productionBefore"] = run.Production.ByteLength,
            ["productionAfter"] = run.Plan is null ? null : plan.Growth.ProjectedBytes,
            ["savedBytes"] = run.Plan is null ? null : plan.TotalEstimatedSaving,
        };
    }

    /// <summary>La classe de sûreté, dans le vocabulaire publié du contrat de sortie.</summary>
    private static string Safety(Audit.SafetyClass safety) => safety switch
    {
        Audit.SafetyClass.Safe => "SAFE",
        Audit.SafetyClass.ReviewRequired => "REVIEW_REQUIRED",
        _ => "UNSAFE",
    };

    private static JsonObject Inputs(ApiDeltaRun run)
    {
        JsonObject inputs = new()
        {
            ["production"] = Describe(run.Production),
        };

        if (run.Candidate is { } candidate)
        {
            inputs["candidate"] = Describe(candidate);
        }

        return inputs;
    }

    private static JsonObject Describe(SpecDocument document) => new()
    {
        ["path"] = document.Path,
        ["format"] = document.Format.ToString().ToLowerInvariant(),
        ["openApiVersion"] = document.DeclaredVersion.ToString(),
        ["bytes"] = document.ByteLength,
        ["sha256"] = document.ContentHash,
    };

    private static JsonArray Diagnostics(ApiDeltaRun run)
    {
        JsonArray array = [];

        foreach (Diagnostic diagnostic in run.Diagnostics)
        {
            JsonObject entry = new()
            {
                ["code"] = diagnostic.Code,
                ["severity"] = diagnostic.Severity.ToString().ToLowerInvariant(),
                ["message"] = diagnostic.Message,
            };

            if (diagnostic.Location is { } location)
            {
                entry["jsonPointer"] = location.Json;
                entry["displayPath"] = location.Display;
            }

            if (diagnostic.Position is { } position)
            {
                entry["line"] = position.Line;
                entry["column"] = position.Column;
            }

            if (diagnostic.Related.Count > 0)
            {
                JsonArray related = [];

                foreach (Syntax.SourcePosition other in diagnostic.Related)
                {
                    related.Add(new JsonObject
                    {
                        ["line"] = other.Line,
                        ["column"] = other.Column,
                    });
                }

                entry["related"] = related;
            }

            array.Add(entry);
        }

        return array;
    }

    private static JsonArray Differences(ApiDeltaRun run)
    {
        JsonArray array = [];

        foreach (Difference difference in run.Differences)
        {
            JsonObject entry = new()
            {
                ["changeId"] = difference.ChangeId,
                ["jsonPointer"] = difference.Location.Json,
                ["displayPath"] = difference.Location.Display,
                ["kind"] = difference.Kind.ToString().ToLowerInvariant(),
                ["classification"] = difference.Impact.ToString().ToLowerInvariant(),
                ["confidence"] = difference.Confidence.ToString().ToLowerInvariant(),
                ["rationale"] = difference.Rationale,
                ["recommendedAction"] = difference.RecommendedAction,
            };

            if (difference.Before is { } before)
            {
                entry["before"] = Summarise(before);
            }

            if (difference.After is { } after)
            {
                entry["after"] = Summarise(after);
            }

            if (difference.Policy is { } policy)
            {
                entry["policy"] = policy;
            }

            array.Add(entry);
        }

        return array;
    }

    private static JsonObject Summarise(ValueSummary summary) => new()
    {
        ["text"] = summary.Text,
        ["truncated"] = summary.Truncated,
        ["fullLength"] = summary.FullLength,
    };

    /// <summary>La recommandation de version, et de quoi la contester.</summary>
    private static JsonNode Version(ApiDeltaRun run)
    {
        if (run.Version is not { } version)
        {
            return JsonValue.Create("notApplicable");
        }

        JsonObject node = new()
        {
            ["baseline"] = version.Baseline?.ToString(),
            ["increment"] = version.Increment.ToString().ToLowerInvariant(),
            ["recommended"] = version.Recommended?.ToString(),
            ["requiresReview"] = version.RequiresReview,
            ["contributors"] = Contributors(version.Contributors),
            ["unreviewed"] = Contributors(version.Unreviewed),
            ["resolvedPolicy"] = Policy(version.ResolvedPolicy),
        };

        if (version.BaselineProblem is { } problem)
        {
            node["baselineProblem"] = problem;
        }

        if (version.Override is { } imposed)
        {
            node["override"] = new JsonObject
            {
                ["requested"] = imposed.Requested.ToString(),
                ["belowRecommendation"] = imposed.BelowRecommendation,
                ["forced"] = imposed.Forced,
            };
        }

        return node;
    }

    private static JsonArray Contributors(
        IReadOnlyList<Versioning.VersionContributor> contributors)
    {
        JsonArray array = [];

        foreach (Versioning.VersionContributor contributor in contributors)
        {
            array.Add(new JsonObject
            {
                ["changeId"] = contributor.ChangeId,
                ["jsonPointer"] = contributor.Location.Json,
            });
        }

        return array;
    }

    private static JsonObject Policy(
        IReadOnlyDictionary<string, Configuration.VersionImpact> resolved)
    {
        JsonObject node = [];

        foreach ((string key, Configuration.VersionImpact impact) in resolved.OrderBy(
            entry => entry.Key, StringComparer.Ordinal))
        {
            node[key] = impact.ToString().ToLowerInvariant();
        }

        return node;
    }

    private static JsonArray Steps(ApiDeltaRun run)
    {
        JsonArray array = [];

        foreach (RunStepState step in run.Steps)
        {
            JsonObject entry = new()
            {
                ["name"] = step.Name,
                ["status"] = step.Status.ToLowerInvariant(),
            };

            if (step.SkipReason is { } reason)
            {
                entry["skipReason"] = reason;
            }

            array.Add(entry);
        }

        return array;
    }

    private static JsonObject Outcome(ApiDeltaRun run) => new()
    {
        ["code"] = OutcomeCodeNames.Of(run.Code),
        ["breakingChanges"] = run.Breaking.Count(),
        ["unknownChanges"] = run.Unknown.Count(),
        ["differences"] = run.Differences.Count,
    };
}
