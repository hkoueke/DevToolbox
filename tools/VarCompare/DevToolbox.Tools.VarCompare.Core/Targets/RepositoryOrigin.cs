using DevToolbox.Domain.AzureDevOps;

namespace DevToolbox.Tools.VarCompare.Core.Targets;

/// <summary>
/// La collection et le projet déduits du dépôt dans lequel travaille le développeur.
/// </summary>
/// <param name="Collection">
/// La collection, déduite relativement à l'URL de base configurée, afin qu'un répertoire virtuel tel que
/// <c>/tfs</c> ne soit pas pris pour elle.
/// </param>
/// <param name="Project">Le projet, connu par son nom à ce stade.</param>
/// <param name="RepositoryName">Le nom du dépôt, montré pour aider à reconnaître le contexte.</param>
/// <param name="RemoteName">Le dépôt distant dont proviennent ces valeurs, par exemple <c>origin</c>.</param>
/// <param name="SourceUrl">L'URL distante brute, montrée pour que le développeur vérifie la déduction.</param>
public sealed record RepositoryOrigin(
    string Collection,
    ProjectIdentifier Project,
    string RepositoryName,
    string RemoteName,
    string SourceUrl);
