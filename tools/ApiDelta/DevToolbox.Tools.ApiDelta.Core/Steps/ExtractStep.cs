using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Construit l'extrait des opérations retenues, l'émet, et le vérifie.
/// </summary>
/// <remarks>
/// Monter, émettre, vérifier — dans cet ordre, et l'écriture ne vient qu'après. Les octets n'atteignent
/// le magasin d'artefacts que si les cinq épreuves ont abouti : un extrait qui aurait perdu un paramètre
/// de chemin se relit, se valide, et décrit autre chose, si bien qu'il n'y a aucun contrôle moins coûteux
/// qui le remarquerait.
/// <para>
/// Rejouable : construire un extrait ne touche à rien. La source n'est jamais réécrite dans ce mode, et
/// il n'y a pas de chemin d'ici vers l'écriture sur place.
/// </para>
/// </remarks>
public sealed class ExtractStep : IToolStep
{
    private readonly IExtractBuilder _builder;
    private readonly ISpecEmitter _emitter;
    private readonly IExtractVerifier _verifier;
    private readonly IOperationSelectionPrompt _prompt;
    private readonly ApiDeltaOptions _options;
    private readonly IClock _clock;

    /// <summary>Crée l'étape.</summary>
    /// <param name="builder">Monte l'extrait.</param>
    /// <param name="emitter">L'émet.</param>
    /// <param name="verifier">Établit qu'il dit la même chose.</param>
    /// <param name="prompt">Demande quelles opérations retenir, la description une fois lue.</param>
    /// <param name="options">Ce que la racine de composition fixe.</param>
    /// <param name="clock">Fournit l'horodatage de la marque de provenance.</param>
    public ExtractStep(
        IExtractBuilder builder,
        ISpecEmitter emitter,
        IExtractVerifier verifier,
        IOperationSelectionPrompt prompt,
        ApiDeltaOptions options,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _builder = builder;
        _emitter = emitter;
        _verifier = verifier;
        _prompt = prompt;
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Extract;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);
        SpecDocument? source = context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument);

        if (selection is null || source is null)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput, "Il n'y a rien à extraire : aucune description n'a été lue."));
        }

        Ruleset ruleset = context.Get<Ruleset>(ApiDeltaContextKeys.Ruleset)
            ?? _options.DefaultRuleset;

        // Les opérations reprises d'une exécution interrompue sont employées telles quelles : c'est tout
        // l'intérêt de les avoir mémorisées. Le constructeur refusera en les nommant si l'une d'elles a
        // disparu de la description depuis.
        IReadOnlyList<OperationSelector> selectors = selection.SelectedOperations.Count > 0
            ? selection.SelectedOperations
            : _prompt.Ask(source);

        if (selectors.Count == 0)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.Cancelled,
                "Aucune opération n'a été retenue : il n'existe pas d'extrait vide."));
        }

        context.Set(
            ApiDeltaContextKeys.Selection, selection with { SelectedOperations = selectors });

        ExtractRequest request = new(
            selectors,
            ruleset.Extract.StampProvenance,
            ruleset.Extract.DefaultName);

        Result<BuiltExtract> built = _builder.Build(source, request, cancellationToken);

        if (built.IsFailure)
        {
            return Task.FromResult(Result.Fail(built.Failure!));
        }

        return Task.FromResult(Finish(context, source, request, ruleset, built.Value));
    }

    /// <summary>Marque, émet, vérifie, et dépose le résultat.</summary>
    /// <remarks>
    /// Le résultat est déposé même lorsque la vérification échoue : il porte alors l'explication, et
    /// c'est elle que le rapport affichera. Ne rien déposer laisserait un refus muet.
    /// </remarks>
    private Result Finish(
        ToolRunContext context,
        SpecDocument source,
        ExtractRequest request,
        Ruleset ruleset,
        BuiltExtract built)
    {
        ExtractProvenance provenance = new(
            source.Path, source.ContentHash, _clock.UtcNow, request.Selectors);

        SpecNode document = request.StampProvenance
            ? ProvenanceStamp.Apply(built.Document, provenance)
            : built.Document;

        ReadOnlyMemory<byte> serialized = _emitter.Emit(document, ruleset.Formatting);

        ExtractVerification verification = _verifier.Verify(
            serialized, source, request.Selectors, ruleset);

        ExtractResult result = new(
            document,
            verification.Established ? serialized : ReadOnlyMemory<byte>.Empty,
            built.Closure,
            built.CarriedTags,
            built.InheritedContext,
            ExtensionCarryOver.Dangling(document, request.Selectors),
            provenance,
            verification);

        context.Set(ApiDeltaContextKeys.Extract, result);

        return verification.Established
            ? Result.Success()
            : Result.Fail(FailureReason.InvalidInput, verification.Explanation);
    }
}
