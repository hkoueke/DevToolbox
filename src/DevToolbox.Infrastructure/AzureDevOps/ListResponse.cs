using System.Text.Json.Serialization;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// L'enveloppe dans laquelle Azure DevOps emballe ses réponses de liste. Contrat explicite, sans
/// désérialisation polymorphe.
/// </summary>
/// <typeparam name="T">Le type des éléments.</typeparam>
public sealed class ListResponse<T>
{
    /// <summary>Le nombre d'éléments de cette page.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>Les éléments de cette page.</summary>
    [JsonPropertyName("value")]
    public IReadOnlyList<T> Value { get; set; } = [];
}
