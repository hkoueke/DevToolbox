using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>Projette le contrat, propose un appariement, le fait confirmer, puis établit la couverture.</summary>
/// <remarks>
/// L'ordre de ces quatre gestes est la règle la plus importante de cette tranche : <strong>rien ne se
/// compare avant confirmation</strong>. Aucune convention ne fait le pont entre un contrat de service et
/// une description REST, si bien qu'une correspondance que l'outil aurait inventée sans la montrer
/// rendrait infalsifiable chaque constat posé dessus.
/// <para>
/// La couverture est établie ici, et non par le comparateur. Elle repose sur la correspondance confirmée
/// et sur rien d'autre, ce qui la rend certaine là où la compatibilité porte les réserves de la
/// projection — deux questions, deux certitudes, deux sections du rapport.
/// </para>
/// <para>
/// Renoncer réussit. L'étape dépose alors une correspondance non confirmée et une couverture non établie,
/// et la suite n'a rien à comparer. Échouer ici reviendrait à punir un développeur d'avoir lu.
/// </para>
/// </remarks>
public sealed class CorrespondStep : IToolStep
{
    private readonly IWsdlProjector _projector;
    private readonly ICorrespondenceProposer _proposer;
    private readonly ICorrespondencePrompt _prompt;

    /// <summary>Crée l'étape.</summary>
    /// <param name="projector">Projette le contrat.</param>
    /// <param name="proposer">Propose l'appariement.</param>
    /// <param name="prompt">Le fait confirmer.</param>
    public CorrespondStep(
        IWsdlProjector projector, ICorrespondenceProposer proposer, ICorrespondencePrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(proposer);
        ArgumentNullException.ThrowIfNull(prompt);

        _projector = projector;
        _proposer = proposer;
        _prompt = prompt;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Correspond;

    /// <inheritdoc />
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Get<WsdlContract>(ApiDeltaContextKeys.WsdlContract) is not { } contract)
        {
            // Pas de contrat de service : le candidat est une description OpenAPI, et cette étape n'a
            // rien à dire.
            return Result.Success();
        }

        if (context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument) is not { } target)
        {
            return Result.Fail(
                FailureReason.InvalidInput,
                "La production n'a pas été lue : il n'y a rien à quoi apparier les opérations du "
                    + "contrat de service.");
        }

        // Les routes sont relevées ici plutôt que réclamées à une étape de 003. Ajouter un dépôt à une
        // étape existante pour le seul besoin de celle-ci lierait les deux, alors que l'arbre de la
        // production est déjà dans le contexte et porte tout ce qu'il faut.
        SpecDocumentRoutes production = SpecDocumentRoutes.From(target);

        context.Set(ApiDeltaContextKeys.ProductionRoutes, production);

        ProjectionResult projected = _projector.Project(
            contract,
            production.Version,
            (context.Get<Configuration.Ruleset>(ApiDeltaContextKeys.Ruleset)
                ?? Configuration.Ruleset.Default).Wsdl.Table);

        context.Set(ApiDeltaContextKeys.WsdlProjection, projected);
        context.Set(ApiDeltaContextKeys.WsdlProjectionGaps, projected.Gaps);

        return await ConfirmAsync(context, contract, production, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// L'appariement qu'une exécution interrompue avait déjà fait confirmer, s'il y en a un.
    /// </summary>
    /// <remarks>
    /// Employé tel quel, et c'est tout l'intérêt de l'avoir mémorisé : reposer la question obligerait le
    /// développeur à refaire, opération par opération, un travail qu'il a déjà fait — et c'est au moment
    /// de reprendre qu'il a le moins envie de le refaire.
    /// <para>
    /// Seul un appariement <em>confirmé</em> revient : c'est la seule forme que le point de reprise
    /// accepte d'écrire, et une proposition non confirmée n'est pas une décision qu'on puisse restituer
    /// à la place de quelqu'un.
    /// </para>
    /// </remarks>
    private static OperationCorrespondence? Resumed(ToolRunContext context) =>
        context.Get<RunSelection>(ApiDeltaContextKeys.Selection)?.Correspondence
            is { IsConfirmed: true } confirmed
            ? confirmed
            : null;

    /// <summary>Propose l'appariement, et le fait confirmer.</summary>
    private Task<OperationCorrespondence> Confirm(
        IReadOnlyList<string> operations,
        SpecDocumentRoutes production,
        ToolRunContext context,
        CancellationToken cancellationToken)
    {
        OperationCorrespondence proposed = _proposer.Propose(
            operations,
            production.Routes,
            context.Get<IReadOnlyDictionary<string, string>>(ApiDeltaContextKeys.DeclaredCorrespondence));

        return _prompt.ConfirmAsync(proposed, production.Routes, cancellationToken);
    }

    /// <summary>Propose, fait confirmer, puis établit la couverture — dans cet ordre.</summary>
    private async Task<Result> ConfirmAsync(
        ToolRunContext context,
        WsdlContract contract,
        SpecDocumentRoutes production,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> operations = [.. contract.Operations.Select(operation => operation.Name)];

        OperationCorrespondence settled = Resumed(context) ?? await Confirm(
            operations, production, context, cancellationToken).ConfigureAwait(false);

        context.Set(ApiDeltaContextKeys.Correspondence, settled);

        CoverageReport coverage = CoverageReport.Establish(
            settled, operations, new HashSet<string>(production.Routes, StringComparer.Ordinal));

        context.Set(ApiDeltaContextKeys.Coverage, coverage);

        // La base que la suite consommera : d'où chaque élément projeté vient, sur quel appariement il
        // repose, et ce que la production en couvre déjà. Déposée ici parce que c'est le seul endroit où
        // les trois existent en même temps.
        if (context.Get<ProjectionResult>(ApiDeltaContextKeys.WsdlProjection) is { } projected)
        {
            context.Set(
                ApiDeltaContextKeys.WsdlFooting,
                new Correction.WsdlFooting(
                    DerivationIndex.From(contract, projected), settled, coverage, projected));
        }

        return Result.Success();
    }
}
