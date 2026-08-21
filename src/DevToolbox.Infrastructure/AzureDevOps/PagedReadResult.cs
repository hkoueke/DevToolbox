namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>Une page d'une réponse de liste Azure DevOps.</summary>
/// <typeparam name="T">Le type des éléments.</typeparam>
/// <param name="Items">Les éléments de cette page.</param>
/// <param name="ContinuationToken">
/// La valeur de l'en-tête de réponse <c>x-ms-continuationtoken</c>, ou <see langword="null"/> s'il s'agit de
/// la dernière page. La pagination est suivie jusqu'au bout : une liste partielle est un échec, jamais un
/// succès tronqué.
/// </param>
public sealed record PagedReadResult<T>(IReadOnlyList<T> Items, string? ContinuationToken);
