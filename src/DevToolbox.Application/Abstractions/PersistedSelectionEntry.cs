namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Un élément de la sélection d'une exécution interrompue : de quoi le retrouver, et de quoi le nommer.
/// </summary>
/// <param name="Kind">
/// La sorte de chose dont il s'agit, dans le vocabulaire de l'outil : <c>groupe</c>, <c>production</c>,
/// <c>operation</c>. La couche partagée ne l'interprète pas ; elle la restitue telle quelle.
/// </param>
/// <param name="Key">
/// Ce dont l'outil a besoin pour retrouver la chose quand il la relira : un identifiant, un chemin, une
/// méthode et un chemin d'opération. Jamais le contenu de la chose elle-même.
/// </param>
/// <param name="Label">Ce que l'invite de reprise affiche, pour que l'outil n'ait pas à être consulté.</param>
/// <remarks>
/// Trois chaînes, et rien d'autre. C'est ce qui permet à un point de reprise de servir n'importe quel
/// outil sans que la couche partagée apprenne ce qu'est un groupe de variables ou une description OpenAPI,
/// et c'est aussi ce qui rend structurelle la promesse qu'aucune valeur ne survit à la session : il n'y a
/// pas de membre ici capable d'en porter une.
/// </remarks>
public sealed record PersistedSelectionEntry(string Kind, string Key, string Label);
