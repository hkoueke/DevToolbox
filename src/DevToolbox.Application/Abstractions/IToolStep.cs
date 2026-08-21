using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;

namespace DevToolbox.Application.Abstractions;

/// <summary>Une étape nommée d'une exécution, reprenable individuellement.</summary>
public interface IToolStep
{
    /// <summary>
    /// Le nom de l'étape, qui apparaît dans les journaux et dans l'invite de reprise. Pour les étapes
    /// engendrées par élément, le nom porte l'élément, par exemple <c>RetrieveGroup:42</c>.
    /// </summary>
    string Name { get; }

    /// <summary>Indique s'il est sans danger de réexécuter cette étape.</summary>
    /// <remarks>
    /// Toutes les étapes de varcompare renvoient <see langword="true"/> puisqu'il ne s'agit que de lectures.
    /// Le drapeau vit malgré tout sur l'abstraction partagée : une étape qu'on ne peut pas rejouer sans
    /// risque doit pouvoir se déclarer non reprenable, et le second outil en comportera.
    /// </remarks>
    bool IsIdempotent { get; }

    /// <summary>Exécute l'étape.</summary>
    /// <param name="context">Le contexte d'exécution, porteur de ce qu'ont produit les étapes précédentes.</param>
    /// <param name="cancellationToken">Annule l'étape de manière coopérative.</param>
    /// <returns>Un succès, ou un échec dont le message est déjà affichable tel quel.</returns>
    Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken);
}
