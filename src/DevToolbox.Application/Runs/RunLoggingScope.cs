using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using Microsoft.Extensions.Logging;

namespace DevToolbox.Application.Runs;

/// <summary>
/// Ouvre la portée de journalisation qui relie entre elles toutes les lignes d'une exécution.
/// </summary>
public static class RunLoggingScope
{
    /// <summary>La clé de portée portant l'identifiant de corrélation de l'exécution.</summary>
    public const string RunIdKey = "RunId";

    /// <summary>La clé de portée portant l'identifiant de l'outil.</summary>
    public const string ToolIdKey = "ToolId";

    /// <summary>La clé de portée portant la collection lue.</summary>
    public const string CollectionKey = "Collection";

    /// <summary>La clé de portée portant le projet lu.</summary>
    public const string ProjectKey = "Project";

    /// <summary>Ouvre une portée porteuse de l'identité d'une exécution.</summary>
    /// <param name="logger">Le journal à cadrer.</param>
    /// <param name="run">L'exécution en cours.</param>
    /// <param name="collection">La collection, lorsqu'elle est déjà connue.</param>
    /// <param name="project">Le projet, lorsqu'il est déjà connu.</param>
    /// <returns>La portée, à libérer à la fin de l'exécution.</returns>
    public static IDisposable? BeginRunScope(
        this ILogger logger,
        ToolRun run,
        string? collection = null,
        string? project = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(run);

        Dictionary<string, object> state = new(StringComparer.Ordinal)
        {
            [RunIdKey] = run.RunId,
            [ToolIdKey] = run.ToolId,
        };

        if (!string.IsNullOrWhiteSpace(collection))
        {
            state[CollectionKey] = collection;
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            state[ProjectKey] = project;
        }

        return logger.BeginScope(state);
    }
}
