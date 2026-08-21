using System.Globalization;
using DevToolbox.Domain.AzureDevOps;

namespace DevToolbox.Tools.VarCompare.Infrastructure;

/// <summary>
/// Construit les trois routes de lecture. Chaque segment est validé et échappé avant d'atteindre une URL.
/// </summary>
/// <remarks>
/// La collection est prise relativement à l'adresse de base configurée, et non supposée être le premier
/// segment du chemin : une installation située derrière un répertoire virtuel tel que <c>/tfs</c> fonctionne
/// donc sans adaptation.
/// </remarks>
public static class VariableGroupRoutes
{
    /// <summary>Construit la route qui liste les projets de la collection.</summary>
    /// <param name="collection">Le segment de collection validé.</param>
    /// <param name="apiVersion">La version d'API à demander.</param>
    /// <param name="continuationToken">Le jeton de continuation, lors du suivi d'une page.</param>
    /// <returns>Une URL relative.</returns>
    public static string ListProjects(string collection, string apiVersion, string? continuationToken)
    {
        string url = string.Create(
            CultureInfo.InvariantCulture,
            $"{Segment(collection)}/_apis/projects?api-version={Escape(apiVersion)}&$top=100");

        return AppendContinuation(url, continuationToken);
    }

    /// <summary>Construit la route qui liste les groupes de variables d'un projet.</summary>
    /// <param name="collection">Le segment de collection validé.</param>
    /// <param name="project">Le projet à lister.</param>
    /// <param name="apiVersion">La version d'API à demander.</param>
    /// <param name="continuationToken">Le jeton de continuation, lors du suivi d'une page.</param>
    /// <returns>Une URL relative.</returns>
    public static string ListGroups(
        string collection,
        ProjectIdentifier project,
        string apiVersion,
        string? continuationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        string url = string.Create(
            CultureInfo.InvariantCulture,
            $"{Segment(collection)}/{project.ToRouteSegment()}/_apis/distributedtask/variablegroups"
                + $"?api-version={Escape(apiVersion)}");

        return AppendContinuation(url, continuationToken);
    }

    /// <summary>Construit la route qui lit un groupe de variables.</summary>
    /// <param name="collection">Le segment de collection validé.</param>
    /// <param name="project">Le projet auquel le groupe appartient.</param>
    /// <param name="groupId">L'id du groupe. Un entier, qui n'a donc pas besoin d'être échappé.</param>
    /// <param name="apiVersion">La version d'API à demander.</param>
    /// <returns>Une URL relative.</returns>
    public static string GetGroup(
        string collection,
        ProjectIdentifier project,
        int groupId,
        string apiVersion)
    {
        ArgumentNullException.ThrowIfNull(project);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Segment(collection)}/{project.ToRouteSegment()}/_apis/distributedtask/variablegroups/"
                + $"{groupId}?api-version={Escape(apiVersion)}");
    }

    private static string Segment(string collection)
    {
        if (!ServerTarget.IsSafeCollection(collection))
        {
            throw new ArgumentException(
                "The collection is not safe to place in a URL.", nameof(collection));
        }

        return Uri.EscapeDataString(collection);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string AppendContinuation(string url, string? continuationToken) =>
        string.IsNullOrEmpty(continuationToken)
            ? url
            : url + "&continuationToken=" + Uri.EscapeDataString(continuationToken);
}
