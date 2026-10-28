using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Une opération retenue, et tout ce qu'il faut pour en composer la route.</summary>
/// <param name="Operation">Le nom de l'opération, tel que le contrat le déclare.</param>
/// <param name="Fields">Les champs de son entrée : nom, type déclaré, composite ou non.</param>
/// <param name="Suggested">L'itinéraire que la dérivation propose.</param>
/// <param name="Worth">Ce que vaut cette proposition — dérivée, partielle, ou repli.</param>
/// <remarks>
/// Tout ce dont l'invite a besoin, et rien de plus : elle ne reçoit ni le contrat, ni la projection, ni
/// la production. C'est ce qui permet de l'éprouver avec un sujet fabriqué en trois lignes, et ce qui
/// empêche une invite de se mettre à déduire ce que le cœur a déjà déduit.
/// <para>
/// <paramref name="Worth" /> accompagne la proposition parce qu'une suggestion montrée sans sa valeur
/// passe pour un fait. Un repli affiché comme un itinéraire fait passer plus vite un développeur sur une
/// proposition qu'il aurait dû regarder.
/// </para>
/// </remarks>
public sealed record AuthoringSubject(
    string Operation,
    IReadOnlyList<(string Field, string Declared, bool IsComposite)> Fields,
    OperationSelector Suggested,
    RouteDerivationKind Worth)
{
    /// <summary>Les champs, jamais nuls.</summary>
    public IReadOnlyList<(string Field, string Declared, bool IsComposite)> Fields { get; init; } =
        Fields ?? [];

    /// <summary>Indique que l'opération ne déclare aucun champ d'entrée.</summary>
    /// <remarks>
    /// Un cas réel : une opération sans entrée n'a rien à placer, et l'invite doit sauter la question au
    /// lieu de présenter une liste vide.
    /// </remarks>
    public bool HasNoFields => Fields.Count == 0;
}
