using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Steps;

/// <summary>Demande ce que chaque opération retenue doit devenir comme route.</summary>
/// <remarks>
/// L'étape qui remplace une devinette par une question. La dérivation d'un itinéraire depuis le nom d'une
/// opération a été éprouvée sur une paire réelle : dix-neuf routes produites, aucune ne tombant sur son
/// équivalent, parce qu'aucune règle de nommage ne retrouve les conventions de chemin d'une maison.
/// <para>
/// Placée après l'appariement et avant la validation. Elle a besoin du contrat lu, de la projection et de
/// la production — ce que seules les étapes précédentes fournissent — et doit précéder la comparaison,
/// puisque ce qu'elle compose change ce qu'il y a à comparer.
/// </para>
/// <para>
/// L'appariement reste à sa place et répond à une autre question : à quelle route existante une opération
/// couverte correspond-elle. C'est sur lui que repose le rapport de couverture. Composer répond à « que
/// doit être cette route neuve », et fusionner les deux mettrait la couverture en jeu.
/// </para>
/// </remarks>
public sealed class AuthorStep : IToolStep
{
    private readonly IRouteAuthoringPrompt _prompt;

    /// <summary>Crée l'étape.</summary>
    /// <param name="prompt">Demande au développeur ce que les routes doivent être.</param>
    public AuthorStep(IRouteAuthoringPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        _prompt = prompt;
    }

    /// <inheritdoc />
    public string Name => ApiDeltaStepNames.Author;

    /// <inheritdoc />
    /// <remarks>
    /// Idempotente au sens de la chaîne : rejouée, elle repose les questions que le registre n'a pas
    /// déjà résolues, et n'écrit rien d'autre que ce registre.
    /// </remarks>
    public bool IsIdempotent => true;

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Get<WsdlContract>(ApiDeltaContextKeys.WsdlContract) is not { } contract)
        {
            // Le compagnon n'est pas un contrat de service : il porte ses propres routes, et il n'y a
            // rien à composer.
            return Result.Success();
        }

        if (context.Get<ProjectionResult>(ApiDeltaContextKeys.WsdlProjection) is not { } projected
            || context.Get<SpecDocument>(ApiDeltaContextKeys.ProductionDocument) is not { } production)
        {
            return Result.Fail(
                FailureReason.InvalidInput,
                "La projection ou la production manque : il n'y a rien sur quoi composer une route.");
        }

        List<AuthoringSubject> subjects = Subjects(contract, projected);

        if (subjects.Count == 0)
        {
            context.Set(ApiDeltaContextKeys.Authoring, AuthoringRecord.Empty);

            return Result.Success();
        }

        IReadOnlyList<string> paths = Paths(production);

        AuthoringRecord settled = await _prompt.AuthorAsync(
            subjects,
            paths,
            ReusableHeaders.In(production),
            cancellationToken).ConfigureAwait(false);

        context.Set(ApiDeltaContextKeys.Authoring, settled);

        return Established(settled, production);
    }

    /// <summary>Une entrée par opération du contrat, avec ses champs et sa suggestion.</summary>
    /// <remarks>
    /// Toutes les opérations du contrat sont présentées : c'est l'invite qui sait lesquelles le
    /// développeur retient, parce que c'est à elle qu'il le dit. Les filtrer ici demanderait de connaître
    /// une réponse qui n'a pas encore été donnée.
    /// </remarks>
    private static List<AuthoringSubject> Subjects(
        WsdlContract contract, ProjectionResult projected)
    {
        List<AuthoringSubject> subjects = [];

        foreach (WsdlOperation operation in contract.Operations)
        {
            if (!projected.Routes.TryGetValue(operation.Name, out DerivedRoute? derived)
                || !OperationSelector.TryParse(derived.Display, out OperationSelector? suggested)
                || suggested is null)
            {
                continue;
            }

            subjects.Add(new AuthoringSubject(
                operation.Name,
                InputFields.Of(contract, operation),
                suggested,
                derived.Kind));
        }

        return subjects;
    }

    /// <summary>Les chemins de la production, dans l'ordre où elle les écrit.</summary>
    private static IReadOnlyList<string> Paths(SpecDocument production) =>
        PointerNavigator.Find(production.Root, "/paths") is MappingNode paths
            ? [.. paths.Entries.Select(entry => entry.Key.Text)]
            : [];

    /// <summary>Établit les collisions avant que quoi que ce soit ne soit écrit.</summary>
    /// <remarks>
    /// Ici et pas plus tard : une collision découverte à l'écriture aurait déjà fait perdre la séance, et
    /// le développeur doit pouvoir changer sa réponse tant qu'elle n'a rien coûté.
    /// </remarks>
    private static Result Established(AuthoringRecord settled, SpecDocument production)
    {
        IReadOnlyList<string> held =
        [
            .. SpecDocumentRoutes.From(production).Routes,
        ];

        IReadOnlyList<string> collisions = settled.Collisions(held);

        return collisions.Count == 0
            ? Result.Success()
            : Result.Fail(
                FailureReason.InvalidInput,
                "Les routes composées se heurtent à ce qui existe : " + string.Join(" · ", collisions));
    }
}
