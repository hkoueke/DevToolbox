namespace DevToolbox.Application.Abstractions;

/// <summary>La seule partie d'une exécution qui survit au processus.</summary>
/// <param name="RunId">L'identifiant de corrélation de l'exécution.</param>
/// <param name="ToolId">L'outil auquel l'exécution appartient.</param>
/// <param name="Selection">
/// Ce que le développeur avait choisi, exprimé en chaînes que seul l'outil interprète.
/// </param>
/// <param name="CompletedSteps">Les noms des étapes achevées.</param>
/// <param name="StartedAt">Quand l'exécution a démarré.</param>
/// <param name="LastUpdatedAt">Quand le point de reprise a été écrit ; c'est ce qui pilote la péremption.</param>
/// <remarks>
/// Invariant : aucun membre de ce type, même indirectement, ne peut porter une valeur lue sur un serveur
/// ou dans un document. Il n'existe volontairement aucun chemin d'ici vers un cliché de groupe, vers une
/// entrée de variable ou vers un nœud de description, et c'est ce qui rend la garantie structurelle plutôt
/// qu'affaire de discipline. Le magasin vérifie la forme du type à l'exécution et refuse d'écrire s'il a
/// été élargi ; un test inspecte par ailleurs le JSON sérialisé.
/// <para>
/// La sélection est volontairement une liste d'éléments à trois chaînes plutôt qu'un jeu de membres
/// nommés : la couche partagée sert plusieurs outils, dont les sélections n'ont pas la même forme — des
/// groupes de variables pour l'un, deux fichiers et un mode pour l'autre — et elle n'a pas à connaître
/// l'une ni l'autre.
/// </para>
/// </remarks>
public sealed record PersistedRun(
    Guid RunId,
    string ToolId,
    IReadOnlyList<PersistedSelectionEntry> Selection,
    IReadOnlyList<string> CompletedSteps,
    DateTimeOffset StartedAt,
    DateTimeOffset LastUpdatedAt);
