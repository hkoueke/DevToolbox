using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Reporting;
using DevToolbox.Tools.ApiDelta.Core.Versioning;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Écrit les artefacts de l'exécution.
/// </summary>
/// <remarks>
/// La seule étape qui touche au monde, et c'est pourquoi toutes les autres sont librement rejouables.
/// Elle-même l'est, parce que chaque artefact est écrit en entier sous un nom temporaire avant d'être mis
/// en place : une étape interrompue ne laisse donc pas un rapport incomplet portant le nom d'un rapport
/// fini.
/// </remarks>
public sealed class WriteStep : IToolStep
{
    private readonly IArtifactStore _artifacts;
    private readonly IOptimizationApplier _optimizer;
    private readonly IInPlaceWriter _inPlace;
    private readonly ApiDeltaOptions _options;
    private readonly IClock _clock;

    /// <summary>Crée l'étape.</summary>
    /// <param name="artifacts">Écrit les artefacts.</param>
    /// <param name="optimizer">Applique le plan à une copie, et le vérifie.</param>
    /// <param name="inPlace">Réécrit la production sur place, quand on le lui demande et le confirme.</param>
    /// <param name="options">Ce que la racine de composition fixe, dont le jeu de règles par défaut.</param>
    /// <param name="clock">Fournit l'heure, pour la durée consignée dans le rapport.</param>
    public WriteStep(
        IArtifactStore artifacts,
        IOptimizationApplier optimizer,
        IInPlaceWriter inPlace,
        ApiDeltaOptions options,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(optimizer);
        ArgumentNullException.ThrowIfNull(inPlace);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _artifacts = artifacts;
        _optimizer = optimizer;
        _inPlace = inPlace;
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Write;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);
        SpecDocument? production = context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument);

        if (selection is null || production is null)
        {
            return Result.Fail(
                FailureReason.InvalidInput, "Il n'y a rien à écrire : aucune description n'a été lue.");
        }

        OptimizationOutcome? optimization = Optimize(context, selection, production);

        if (optimization is not null)
        {
            context.Set(ApiDeltaContextKeys.Optimization, optimization);
        }

        ApiDeltaRun run = Assemble(context, selection, production, optimization);

        context.Set(ApiDeltaContextKeys.Outcome, OutcomeCodeNames.Of(run.Code));

        IReadOnlyList<Publication> publications =
            [.. Publishers(context, run, selection, cancellationToken)];

        Result prepared = await _artifacts.PrepareAsync(
            [.. publications.Select(publication => publication.FileName)],
            selection,
            cancellationToken).ConfigureAwait(false);

        if (prepared.IsFailure)
        {
            return prepared;
        }

        List<Artifact> written = [];

        foreach (Publication publication in publications)
        {
            Result<Artifact> published = await publication.Publish().ConfigureAwait(false);

            if (published.IsFailure)
            {
                return Result.Fail(published.Failure!);
            }

            written.Add(published.Value);
        }

        context.Set(ApiDeltaContextKeys.Artifacts, written);

        return Unverified(optimization);
    }

    /// <summary>
    /// Applique le plan, quand le développeur l'a demandé.
    /// </summary>
    /// <remarks>
    /// Avant toute écriture, parce que le résultat de la vérification entre dans les rapports. Une
    /// vérification qui échouerait après la publication des rapports obligerait à les réécrire, ou à les
    /// laisser mentir.
    /// </remarks>
    private OptimizationOutcome? Optimize(
        ToolRunContext context,
        RunSelection selection,
        SpecDocument production)
    {
        if (!selection.ApplySafeOptimizations
            || context.Get<OptimizationPlan>(ApiDeltaContextKeys.Plan) is not { } plan)
        {
            return null;
        }

        return _optimizer.Apply(plan, production, selection, Ruleset(context));
    }

    /// <summary>
    /// Fait échouer l'exécution quand ce qui a été appliqué n'a pas pu être prouvé équivalent.
    /// </summary>
    /// <remarks>
    /// Les rapports sont écrits d'abord, et l'échec vient après : le développeur doit pouvoir lire
    /// <em>pourquoi</em> la copie n'a pas été publiée. Échouer avant d'écrire aurait laissé un refus sans
    /// explication, qui est la pire forme de refus.
    /// <para>
    /// Rien n'est appliqué n'est pas un échec : c'est le cas d'un plan dont aucune proposition n'était
    /// autorisée, et il n'y a là rien d'anormal à signaler.
    /// </para>
    /// </remarks>
    private static Result Unverified(OptimizationOutcome? optimization) =>
        optimization is { Applied.Count: > 0, Verification.Established: false }
            ? Result.Fail(FailureReason.InvalidInput, optimization.Verification.Explanation)
            : Result.Success();

    /// <summary>
    /// Les artefacts que cette exécution doit produire, dans l'ordre, et seulement ceux-là.
    /// </summary>
    private IEnumerable<Publication> Publishers(
        ToolRunContext context,
        ApiDeltaRun run,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        yield return Written(
            ArtifactKind.DeltaJson,
            ArtifactNames.DeltaJson,
            JsonReportWriter.Write(run),
            selection,
            cancellationToken);

        if (run.Audit is not null)
        {
            // Un rapport à part : l'examen du fichier en service ne se mêle pas aux changements de
            // contrat, parce qu'un relecteur n'a jamais les deux questions en tête en même temps.
            yield return Written(
                ArtifactKind.AuditReport,
                ArtifactNames.AuditReport,
                MarkdownReportWriter.WriteAudit(run),
                selection,
                cancellationToken);
        }

        if (run.Plan is not null)
        {
            // Un devis, publié même vide : un plan absent et un plan sans proposition ne disent pas la
            // même chose, et seul le fichier écrit permet de les distinguer après coup.
            yield return Written(
                ArtifactKind.OptimizationPlan,
                ArtifactNames.OptimizationPlan,
                OptimizationPlanWriter.Write(run),
                selection,
                cancellationToken);
        }

        if (run.Optimization is { Publishable: true } optimization)
        {
            foreach (Publication publication in Optimized(
                run, optimization, selection, cancellationToken))
            {
                yield return publication;
            }
        }

        if (run.Extract is { } attempted)
        {
            foreach (Publication publication in Extracted(
                run, attempted, selection, cancellationToken))
            {
                yield return publication;
            }
        }

        if (run.Candidate is not null)
        {
            foreach (Publication publication in Compared(context, run, selection, cancellationToken))
            {
                yield return publication;
            }
        }
    }

    /// <summary>Ce qu'une comparaison produit : son rapport, et la production corrigée si on l'a voulue.</summary>
    private IEnumerable<Publication> Compared(
        ToolRunContext context,
        ApiDeltaRun run,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        yield return Written(
            ArtifactKind.DeltaReport,
            ArtifactNames.DeltaReport,
            MarkdownReportWriter.Write(run),
            selection,
            cancellationToken);

        if (context.Get<BuiltCorrection>(ApiDeltaContextKeys.CorrectedDocument) is { } corrected)
        {
            yield return Written(
                ArtifactKind.CorrectedDocument,
                ArtifactNames.CorrectedDocument,
                corrected.Serialized,
                selection,
                cancellationToken);
        }
    }

    /// <summary>Un artefact dont le contenu est déjà prêt.</summary>
    private Publication Written(
        ArtifactKind kind,
        string fileName,
        ReadOnlyMemory<byte> content,
        RunSelection selection,
        CancellationToken cancellationToken) =>
        new(
            fileName,
            () => _artifacts.PublishAsync(kind, fileName, content, selection, cancellationToken));

    /// <summary>
    /// L'extrait, publié seulement si les cinq épreuves ont abouti.
    /// </summary>
    /// <remarks>
    /// Un extrait non vérifié ne sort pas. Il est indiscernable d'un extrait correct — il se relit, il se
    /// valide — jusqu'au jour où quelqu'un s'en sert pour écrire un client.
    /// </remarks>
    private IEnumerable<Publication> Extracted(
        ApiDeltaRun run,
        Extraction.ExtractResult extract,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        // Le rapport même quand la vérification a échoué : c'est lui qui dira pourquoi. Un refus sans
        // explication est la pire forme de refus.
        yield return Written(
            ArtifactKind.Extract,
            ArtifactNames.ExtractReport,
            MarkdownReportWriter.WriteExtract(run),
            selection,
            cancellationToken);

        if (extract.Publishable)
        {
            yield return Written(
                ArtifactKind.Extract,
                ArtifactNames.Extract,
                extract.Serialized,
                selection,
                cancellationToken);
        }
    }

    /// <summary>
    /// La copie optimisée et son manifeste, dans cet ordre et dans la même étape.
    /// </summary>
    /// <remarks>
    /// Dans cet ordre, parce que le manifeste mesure ce qui a réellement été écrit : sa taille et son
    /// empreinte sont celles du fichier, non celles d'une projection. Et dans la même étape, de sorte
    /// qu'un fichier optimisé dont personne ne sait ce qui lui est arrivé ne soit pas un état que cet
    /// outil puisse produire.
    /// </remarks>
    private IEnumerable<Publication> Optimized(
        ApiDeltaRun run,
        OptimizationOutcome optimization,
        RunSelection selection,
        CancellationToken cancellationToken)
    {
        Artifact? copy = null;

        yield return new Publication(ArtifactNames.OptimizedDocument, async () =>
        {
            Result<Artifact> published = await PublishOptimizedAsync(
                optimization, run.Production, selection, cancellationToken).ConfigureAwait(false);

            copy = published.IsSuccess ? published.Value : null;

            return published;
        });

        yield return new Publication(ArtifactNames.Manifest, () => _artifacts.PublishAsync(
            ArtifactKind.Manifest,
            ArtifactNames.Manifest,
            ManifestWriter.Write(Manifest(run, optimization, copy!)),
            selection,
            cancellationToken));
    }

    /// <summary>
    /// Publie la copie optimisée : à côté par défaut, sur place seulement si on l'a demandé.
    /// </summary>
    /// <remarks>
    /// Deux chemins d'écriture et non un paramètre, parce que les garanties ne sont pas les mêmes. Le
    /// magasin d'artefacts est confiné sous le dossier de sortie, ce qui le rend sans danger ;
    /// l'écriture sur place sort de ce confinement et porte ses propres refus — la confirmation qui nomme
    /// le fichier, la sauvegarde, et le contrôle que le fichier est encore celui qui a été lu.
    /// </remarks>
    private async Task<Result<Artifact>> PublishOptimizedAsync(
        OptimizationOutcome optimization,
        SpecDocument production,
        RunSelection selection,
        CancellationToken cancellationToken) =>
        // Jamais sur place pour une extraction : un extrait est une vue, et il n'existe aucun chemin d'ici
        // vers la réécriture de la source.
        selection.InPlace && selection.Mode != RunMode.Extract
            ? await _inPlace.WriteAsync(
                production, optimization.Serialized, selection.Backup, cancellationToken)
                .ConfigureAwait(false)
            : await _artifacts.PublishAsync(
                ArtifactKind.OptimizedDocument,
                ArtifactNames.OptimizedDocument,
                optimization.Serialized,
                selection,
                cancellationToken).ConfigureAwait(false);

    /// <summary>Le manifeste de ce qui a été appliqué, mesuré sur ce qui a réellement été écrit.</summary>
    private static OptimizationManifest Manifest(
        ApiDeltaRun run,
        OptimizationOutcome optimization,
        Artifact copy) =>
        new(
            run.RunId,
            run.Production.Path,
            run.Production.ContentHash,
            run.Production.ByteLength,
            copy.Path,
            copy.Hash,
            copy.ByteLength,
            optimization.Applied);

    /// <summary>Un artefact à écrire : son nom, et ce qui l'écrira.</summary>
    /// <param name="FileName">Son nom de fichier, connu avant que rien ne soit écrit.</param>
    /// <param name="Publish">Ce qui l'écrit, appelé seulement si la préparation a abouti.</param>
    /// <remarks>
    /// Le nom est séparé de l'écriture pour que l'étape puisse annoncer la liste complète avant d'en
    /// écrire un seul. Reconstituer cette liste ailleurs aurait dédoublé les conditions qui décident
    /// quels artefacts une exécution produit, et les deux copies auraient fini par différer.
    /// </remarks>
    private sealed record Publication(string FileName, Func<Task<Result<Artifact>>> Publish);

    /// <summary>Le jeu de règles en vigueur, celui que la sélection a lu et validé.</summary>
    private Configuration.Ruleset Ruleset(ToolRunContext context) =>
        context.Get<Configuration.Ruleset>(ApiDeltaContextKeys.Ruleset) ?? _options.DefaultRuleset;

    /// <summary>Rassemble ce que les étapes ont produit en une exécution que les rapports savent lire.</summary>
    /// <summary>
    /// Rassemble ce que les étapes ont produit, puis résout l'issue sur l'ensemble.
    /// </summary>
    /// <remarks>
    /// Après l'assemblage, et non pendant : le code d'issue dépend de tout — les diagnostics, les
    /// constats, le seuil que l'équipe a fixé, la vérification de la copie. Le calculer au fil de l'eau
    /// aurait dispersé en six endroits une décision qui n'a qu'un seul ordre de priorité.
    /// </remarks>
    private ApiDeltaRun Assemble(
        ToolRunContext context,
        RunSelection selection,
        SpecDocument production,
        OptimizationOutcome? optimization)
    {
        ApiDeltaRun run = new(
            context.Run.RunId,
            selection,
            context.Run.StartedAt,
            _clock.UtcNow - context.Run.StartedAt,
            production,
            context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument),
            DiagnosticCollector.Of(context),
            context.Get<IReadOnlyList<Difference>>(ApiDeltaContextKeys.Differences) ?? [],
            [
                .. context.Run.Steps.Select(step => new RunStepState(
                    step.Name,
                    step.Status.ToString(),
                    step.Status == StepStatus.Skipped
                        ? ApiDeltaStepNames.SkipReason(selection.Mode)
                        : null)),
            ],
            context.Get<VersionRecommendation>(ApiDeltaContextKeys.VersionRecommendation),
            context.Get<Audit.AuditResult>(ApiDeltaContextKeys.Audit),
            context.Get<Optimization.OptimizationPlan>(ApiDeltaContextKeys.Plan),
            optimization,
            context.Get<Extraction.ExtractResult>(ApiDeltaContextKeys.Extract),
            Corrected(context),
            context.Get<WsdlFooting>(ApiDeltaContextKeys.WsdlFooting));

        return run with { Code = OutcomeResolver.Resolve(run, Ruleset(context).Threshold) };
    }

    /// <summary>Ce que la correction a produit, quand l'étape l'a établi.</summary>
    /// <remarks>
    /// Le plan suffit à rendre compte : il existe dès que quelque chose a été retenu, même si rien n'a
    /// été écrit ensuite. C'est ce qui permet au rapport de dire pourquoi, plutôt que de se taire.
    /// </remarks>
    private static CorrectionOutcome? Corrected(ToolRunContext context)
    {
        if (context.Get<CorrectionPlan>(ApiDeltaContextKeys.CorrectionPlan) is not { } plan)
        {
            return null;
        }

        return new CorrectionOutcome(
            plan,
            context.Get<CorrectionVerification>(ApiDeltaContextKeys.CorrectionVerification)
                ?? CorrectionVerification.NotRun,
            context.Get<BuiltCorrection>(ApiDeltaContextKeys.CorrectedDocument) is not null);
    }
}
