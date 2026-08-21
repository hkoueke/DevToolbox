using System.Text.Json.Serialization;

namespace DevToolbox.Tools.VarCompare.Infrastructure.Wire;

/// <summary>Un projet tel qu'il est transmis par le serveur.</summary>
public sealed class ProjectDto
{
    /// <summary>L'id du projet.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>Le nom du projet.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
