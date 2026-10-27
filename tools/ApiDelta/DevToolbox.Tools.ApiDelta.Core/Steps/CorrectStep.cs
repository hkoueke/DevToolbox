using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Versioning;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>
/// Demande ce qui doit entrer en production, l'énonce, le construit, et le vérifie.
/// </summary>
/// <remarks>
/// L'ordre est la garantie. Choisir, puis énoncer, puis faire accepter, puis construire, puis vérifier sur
/// les octets — et l'écriture n'a lieu qu'à l'étape suivante, et seulement si la vérification a abouti.
/// Une production consolidée qui aurait changé une route que personne n'avait retenue se relit, se valide,
/// et décrit autre chose : aucun contrôle moins coûteux ne le remarquerait.
/// <para>
/// Rejouable : rien ici n'écrit. La production n'est jamais réécrite dans ce mode, et il n'existe aucun
/// chemin d'ici vers l'écriture sur place.
/// </para>
/// </remarks>
public sealed class CorrectStep : IToolStep
{
    private readonly IOperationCatalog _catalog;
    private readonly ICorrectionSelectionPrompt _prompt;
    private readonly ICorrectionBuilder _builder;
    private readonly ICorrectionVerifier _verifier;
    private readonly IDependencyResolver _dependencies;
    private readonly ApiDeltaOptions _options;

    /// <summary>Crée l'étape.</summary>
    /// <param name="catalog">Énumère les routes des deux descriptions.</param>
    /// <param name="prompt">Demande ce qui doit entrer, et fait accepter l'énoncé.</param>
    /// <param name="builder">Construit la production consolidée.</param>
    /// <param name="verifier">Établit qu'elle dit ce qu'on lui a demandé, et rien de plus.</param>
    /// <param name="dependencies">Ferme ce dont le contenu retenu a besoin.</param>
    /// <param name="options">Ce que la racine de composition fixe.</param>
    public CorrectStep(
        IOperationCatalog catalog,
        ICorrectionSelectionPrompt prompt,
        ICorrectionBuilder builder,
        ICorrectionVerifier verifier,
        IDependencyResolver dependencies,
        ApiDeltaOptions options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(options);

        _catalog = catalog;
        _prompt = prompt;
        _builder = builder;
        _verifier = verifier;
        _dependencies = dependencies;
        _options = options;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Correct;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        RunSelection? selection = context.Get<RunSelection>(ApiDeltaContextKeys.Selection);
        SpecDocument? production = context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument);
        SpecDocument? candidate = context.Get<SpecDocument>(ApiDeltaContextKeys.CandidateDocument);

        if (selection is null || production is null || candidate is null)
        {
            return Task.FromResult(Result.Fail(
                FailureReason.InvalidInput,
                "Il n'y a rien à corriger : les deux descriptions n'ont pas été lues."));
        }

        IReadOnlyList<Difference> differences =
            context.Get<IReadOnlyList<Difference>>(ApiDeltaContextKeys.Differences) ?? [];

        return Task.FromResult(Run(context, selection, production, candidate, differences, cancellationToken));
    }

    /// <summary>Choisit, énonce, construit, vérifie.</summary>
    private Result Run(
        ToolRunContext context,
        RunSelection selection,
        SpecDocument production,
        SpecDocument candidate,
        IReadOnlyList<Difference> differences,
        CancellationToken cancellationToken)
    {
        WsdlFooting? footing = context.Get<WsdlFooting>(ApiDeltaContextKeys.WsdlFooting);
        CorrectionSelection chosen = Chosen(selection, production, candidate, differences, footing);

        if (chosen.IsEmpty)
        {
            // Une sélection vide n'est pas un échec : c'est une demande de ne rien écrire, et l'étape
            // réussit en n'ayant rien produit. L'écriture ne trouvera donc aucune production consolidée.
            return Result.Success();
        }

        context.Set(ApiDeltaContextKeys.Selection, selection with { Correction = chosen });

        Result<DependencyClosure> closure = _dependencies.Close(
            [.. Roots(chosen, differences)], candidate, production, cancellationToken);

        if (closure.IsFailure)
        {
            return Result.Fail(closure.Failure!);
        }

        CorrectionRequest request = new(
            production,
            candidate,
            differences,
            chosen,
            closure.Value,
            context.Get<VersionRecommendation>(ApiDeltaContextKeys.VersionRecommendation),
            _options.DefaultRuleset.Formatting,
            selection.IncludeRemovals,
            footing);

        return Announce(context, request, Ruleset(context));
    }

    /// <summary>Énonce ce qui serait inséré, puis construit si le développeur accepte.</summary>
    private Result Announce(ToolRunContext context, CorrectionRequest request, Ruleset ruleset)
    {
        Result<CorrectionPlan> plan = _builder.Plan(request);

        if (plan.IsFailure)
        {
            return Result.Fail(plan.Failure!);
        }

        context.Set(ApiDeltaContextKeys.CorrectionPlan, plan.Value);

        if (plan.Value.IsEmpty)
        {
            // Tout ce qui avait été retenu a été écarté. Ce n'est pas un échec : le plan porte la raison
            // de chaque refus, le rapport les nommera, et il n'y a simplement rien à écrire. Faire
            // échouer l'étape dirait au développeur qu'il s'est trompé, alors que l'outil lui doit
            // d'abord de dire pourquoi.
            return Result.Success();
        }

        if (!_prompt.Confirm(plan.Value))
        {
            return Result.Success();
        }

        Result<BuiltCorrection> built = _builder.Build(request);

        if (built.IsFailure)
        {
            return Result.Fail(built.Failure!);
        }

        CorrectionVerification verification = _verifier.Verify(
            built.Value.Serialized, request.Production, built.Value, ruleset);

        context.Set(ApiDeltaContextKeys.CorrectionVerification, verification);

        if (!verification.Established)
        {
            return Result.Fail(FailureReason.InvalidInput, verification.Explanation);
        }

        context.Set(ApiDeltaContextKeys.ConsolidatedDocument, built.Value);

        return Result.Success();
    }

    /// <summary>Ce que le développeur a retenu, repris d'une exécution interrompue ou demandé.</summary>
    /// <remarks>
    /// Une sélection reprise est employée telle quelle : c'est tout l'intérêt de l'avoir mémorisée, et
    /// reposer la question obligerait le développeur à refaire un choix qu'il avait déjà fait. Le
    /// constructeur écartera en le nommant ce qui aurait disparu des descriptions depuis.
    /// </remarks>
    private CorrectionSelection Chosen(
        RunSelection selection,
        SpecDocument production,
        SpecDocument candidate,
        IReadOnlyList<Difference> differences,
        WsdlFooting? footing)
    {
        if (selection.Correction is { IsEmpty: false } resumed)
        {
            return resumed;
        }

        return _prompt.Ask(
            CorrectionCatalog.Build(production, candidate, differences, _catalog, footing));
    }

    /// <summary>Les emplacements dont il faut fermer les dépendances.</summary>
    private static IEnumerable<SpecPointer> Roots(
        CorrectionSelection chosen, IReadOnlyList<Difference> differences)
    {
        foreach (OperationSelector route in chosen.Routes)
        {
            yield return SpecPointer.Root.Child("paths").Child(route.Path).Child(route.MethodKey);
        }

        foreach (DifferenceSelector selector in chosen.Differences)
        {
            Difference? match = differences.FirstOrDefault(difference =>
                string.Equals(difference.ChangeId, selector.ChangeId, StringComparison.Ordinal)
                && string.Equals(difference.Location.Json, selector.At, StringComparison.Ordinal));

            if (match is not null)
            {
                yield return match.Location;
            }
        }
    }

    private Ruleset Ruleset(ToolRunContext context) =>
        context.Get<Ruleset>(ApiDeltaContextKeys.Ruleset) ?? _options.DefaultRuleset;
}
