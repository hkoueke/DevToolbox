using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.AzureDevOps;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Targets;
using Microsoft.Extensions.Options;

namespace DevToolbox.Tools.VarCompare.Infrastructure;

/// <summary>
/// Détermine dans quel projet se trouve le développeur en lisant le dépôt distant git.
/// </summary>
/// <remarks>
/// <para>
/// La configuration du dépôt est analysée directement plutôt qu'en lançant <c>git</c> : sans création de
/// processus, il n'y a ni surface d'injection d'arguments ni dépendance à la présence de git dans le PATH,
/// et un petit fichier INI se teste trivialement avec des jeux d'essai.
/// </para>
/// <para>
/// Un dépôt distant n'est accepté que si son hôte correspond au serveur configuré. Tout le reste — pas de
/// dépôt, pas de dépôt distant, plusieurs dépôts distants, un hôte étranger — ne renvoie rien, et l'outil
/// interroge au lieu de deviner.
/// </para>
/// </remarks>
public sealed class WorkingFolderInspector : IWorkingFolderInspector
{
    private const string GitDirectoryName = ".git";
    private const string GitRepositoryMarker = "_git";

    private readonly AzureDevOpsServerOptions _options;

    /// <summary>Crée l'inspecteur.</summary>
    /// <param name="options">Les options serveur validées, dont l'hôte conditionne l'acceptation.</param>
    public WorkingFolderInspector(IOptions<AzureDevOpsServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public Result<RepositoryOrigin?> DetectOrigin(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return Result.Success<RepositoryOrigin?>(null);
        }

        string? configPath = FindGitConfig(startDirectory);

        if (configPath is null)
        {
            return Result.Success<RepositoryOrigin?>(null);
        }

        IReadOnlyList<(string Remote, string Url)> remotes;

        try
        {
            remotes = ParseRemotes(File.ReadAllLines(configPath));
        }
        catch (IOException)
        {
            return Result.Success<RepositoryOrigin?>(null);
        }

        // Plusieurs dépôts distants également plausibles : c'est précisément le cas où deviner serait faux.
        List<(string Remote, string Url)> candidates =
            [.. remotes.Where(remote => IsConfiguredServer(remote.Url))];

        if (candidates.Count != 1)
        {
            return Result.Success<RepositoryOrigin?>(null);
        }

        return Result.Success(ToOrigin(candidates[0].Remote, candidates[0].Url));
    }

    /// <summary>Extrait les entrées <c>[remote "nom"] url</c> d'un fichier de configuration git.</summary>
    /// <param name="lines">Les lignes du fichier.</param>
    /// <returns>Chaque dépôt distant et son URL, dans l'ordre du fichier.</returns>
    internal static IReadOnlyList<(string Remote, string Url)> ParseRemotes(IReadOnlyList<string> lines)
    {
        List<(string Remote, string Url)> remotes = [];
        string? currentRemote = null;

        foreach (string raw in lines)
        {
            string line = raw.Trim();

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentRemote = ReadRemoteName(line);
                continue;
            }

            if (currentRemote is null || !line.StartsWith("url", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0)
            {
                string url = line[(separator + 1)..].Trim();

                if (url.Length > 0)
                {
                    remotes.Add((currentRemote, url));
                }
            }
        }

        return remotes;
    }

    /// <summary>
    /// Déduit la collection et le projet d'une URL distante, relativement au chemin de base configuré, afin
    /// qu'un répertoire virtuel tel que <c>/tfs</c> ne soit pas pris pour la collection.
    /// </summary>
    /// <param name="remoteUrl">L'URL distante.</param>
    /// <param name="baseUrl">L'URL de base du serveur configuré.</param>
    /// <returns>La collection, le projet et le dépôt, ou <see langword="null"/> si l'URL est méconnaissable.</returns>
    internal static (string Collection, string Project, string Repository)? DeriveFromUrl(
        string remoteUrl,
        Uri baseUrl)
    {
        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string[] remoteSegments = SplitSegments(uri.AbsolutePath);
        string[] baseSegments = SplitSegments(baseUrl.AbsolutePath);

        // Passer tout ce que l'URL de base couvre déjà. La collection est ce qui vient ensuite.
        int offset = 0;

        while (offset < baseSegments.Length
            && offset < remoteSegments.Length
            && string.Equals(baseSegments[offset], remoteSegments[offset], StringComparison.OrdinalIgnoreCase))
        {
            offset++;
        }

        string[] tail = [.. remoteSegments.Skip(offset)];

        // Forme attendue à partir d'ici : {collection}/{projet}/_git/{dépôt}
        int markerIndex = Array.FindIndex(
            tail, segment => string.Equals(segment, GitRepositoryMarker, StringComparison.OrdinalIgnoreCase));

        if (markerIndex < 2 || markerIndex + 1 >= tail.Length)
        {
            return null;
        }

        string collection = Uri.UnescapeDataString(tail[markerIndex - 2]);
        string project = Uri.UnescapeDataString(tail[markerIndex - 1]);
        string repository = Uri.UnescapeDataString(tail[markerIndex + 1]);

        return ServerTarget.IsSafeCollection(collection) && ProjectIdentifier.IsSafeSegment(project)
            ? (collection, project, repository)
            : null;
    }

    private static string[] SplitSegments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? ReadRemoteName(string sectionLine)
    {
        // [remote "origin"]
        if (!sectionLine.StartsWith("[remote", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int firstQuote = sectionLine.IndexOf('"', StringComparison.Ordinal);
        int lastQuote = sectionLine.LastIndexOf('"');

        return firstQuote >= 0 && lastQuote > firstQuote
            ? sectionLine[(firstQuote + 1)..lastQuote]
            : null;
    }

    private static string? FindGitConfig(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, GitDirectoryName, "config");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private bool IsConfiguredServer(string remoteUrl)
    {
        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out Uri? baseUri)
            && string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private RepositoryOrigin? ToOrigin(string remoteName, string remoteUrl)
    {
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            return null;
        }

        if (DeriveFromUrl(remoteUrl, baseUri) is not { } derived)
        {
            return null;
        }

        return new RepositoryOrigin(
            derived.Collection,
            new ProjectIdentifier(derived.Project),
            derived.Repository,
            remoteName,
            remoteUrl);
    }
}
