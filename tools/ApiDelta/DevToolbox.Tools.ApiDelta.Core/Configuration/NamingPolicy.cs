namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Les conventions de nommage qu'une équipe s'est données.</summary>
/// <param name="PathSegments">La forme d'un segment de chemin.</param>
/// <param name="Components">La forme d'un nom de composant.</param>
/// <param name="OperationIds">La forme d'un identifiant d'opération.</param>
/// <param name="Properties">La forme d'un nom de propriété.</param>
/// <remarks>
/// Rien par défaut, et c'est voulu : une convention est un choix d'équipe, et l'outil qui en imposerait
/// une se mettrait à signaler des centaines de constats sur un corpus qui ne lui a rien demandé. Le défaut
/// n'énonce donc aucune convention, et la règle reste silencieuse jusqu'à ce qu'on lui en donne une.
/// </remarks>
public sealed record NamingPolicy(
    NamingStyle PathSegments = NamingStyle.Any,
    NamingStyle Components = NamingStyle.Any,
    NamingStyle OperationIds = NamingStyle.Any,
    NamingStyle Properties = NamingStyle.Any)
{
    /// <summary>Les conventions par défaut : aucune.</summary>
    public static NamingPolicy Default { get; } = new();
}
