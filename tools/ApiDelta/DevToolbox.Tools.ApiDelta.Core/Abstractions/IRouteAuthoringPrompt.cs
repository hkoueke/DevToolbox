using DevToolbox.Tools.ApiDelta.Core.Authoring;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Demande au développeur ce qu'une route neuve doit être.</summary>
/// <remarks>
/// Un port distinct de <see cref="ICorrespondencePrompt" />, et c'est délibéré : les deux répondent à des
/// questions différentes. L'appariement demande « à quelle route existante cette opération correspond-elle
/// ? » ; la composition demande « que doit être cette route neuve ? ». Une interface qui porterait les
/// deux coupleait l'étape qui apparie les opérations couvertes à celle qui compose les absentes, et le
/// principe II du dépôt rejette une interface qui accumule des opérations sans rapport.
/// <para>
/// Ce port est le seul endroit par lequel un chemin, un verbe ou un placement peuvent entrer. Rien dans le
/// cœur ne les invente : le cœur sait valider, proposer et refuser, mais pas décider. C'est ce qui permet
/// d'éprouver toutes les règles de composition sans console.
/// </para>
/// <para>
/// La validation appartient au cœur, et l'implémentation l'appelle plutôt que de la refaire. Une invite
/// qui porterait ses propres règles finirait par en avoir d'autres que celles du cœur, et le document
/// écrit obéirait aux siennes.
/// </para>
/// </remarks>
public interface IRouteAuthoringPrompt
{
    /// <summary>Compose les routes des opérations retenues.</summary>
    /// <param name="subjects">Une entrée par opération retenue, avec ses champs et sa suggestion.</param>
    /// <param name="productionPaths">
    /// Les chemins de la production, dans l'ordre où elle les écrit : c'est l'ordre dans lequel les points
    /// d'insertion sont offerts, pour que le développeur choisisse un voisin qu'il lit.
    /// </param>
    /// <param name="headers">
    /// Les en-têtes que la production déclare en composants réutilisables, le plus référencé d'abord.
    /// Vide quand elle n'en déclare aucun, et rien n'est alors proposé.
    /// </param>
    /// <param name="cancellationToken">Annule la séance, en gardant ce qui a été arrêté.</param>
    /// <returns>Le registre des routes composées, vide si rien n'a été retenu.</returns>
    /// <remarks>
    /// Rien n'est demandé pour une opération absente de <paramref name="subjects" />, et rien du tout
    /// quand la liste est vide : une question posée pour une opération que le développeur n'a pas retenue
    /// est une cérémonie.
    /// <para>
    /// L'annulation rend ce qui a été arrêté jusque-là plutôt que de le jeter. Composer vingt et une
    /// opérations est une longue séance, et une interruption ne doit pas coûter ce qui était déjà décidé.
    /// </para>
    /// </remarks>
    Task<AuthoringRecord> AuthorAsync(
        IReadOnlyList<AuthoringSubject> subjects,
        IReadOnlyList<string> productionPaths,
        IReadOnlyList<OfferedHeader> headers,
        CancellationToken cancellationToken);
}
