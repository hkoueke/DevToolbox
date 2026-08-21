namespace DevToolbox.Application.Abstractions;

/// <summary>La seule partie d'une exécution qui survit au processus.</summary>
/// <param name="RunId">L'identifiant de corrélation de l'exécution.</param>
/// <param name="ToolId">L'outil auquel l'exécution appartient.</param>
/// <param name="Collection">La collection qui était en cours de lecture.</param>
/// <param name="Project">Le projet qui était en cours de lecture.</param>
/// <param name="SelectedGroups">Les groupes choisis par le développeur, par id et par nom.</param>
/// <param name="CompletedSteps">Les noms des étapes achevées.</param>
/// <param name="StartedAt">Quand l'exécution a démarré.</param>
/// <param name="LastUpdatedAt">Quand le point de reprise a été écrit ; c'est ce qui pilote la péremption.</param>
/// <remarks>
/// Invariant : aucun membre de ce type, même indirectement, ne peut porter une valeur de variable. Il
/// n'existe volontairement aucun chemin d'ici vers un cliché de groupe ou vers une entrée de variable, et
/// c'est ce qui rend la garantie structurelle plutôt qu'affaire de discipline. Un test inspecte le JSON
/// sérialisé pour le vérifier.
/// </remarks>
public sealed record PersistedRun(
    Guid RunId,
    string ToolId,
    string Collection,
    string Project,
    IReadOnlyList<PersistedGroupSelection> SelectedGroups,
    IReadOnlyList<string> CompletedSteps,
    DateTimeOffset StartedAt,
    DateTimeOffset LastUpdatedAt);
