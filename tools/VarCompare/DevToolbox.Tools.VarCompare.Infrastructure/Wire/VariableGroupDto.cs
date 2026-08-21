using System.Text.Json.Serialization;

namespace DevToolbox.Tools.VarCompare.Infrastructure.Wire;

/// <summary>
/// Un groupe de variables tel qu'il est transmis. Contrat explicite, champs inconnus ignorés, aucune
/// désérialisation polymorphe.
/// </summary>
public sealed class VariableGroupDto
{
    /// <summary>L'identifiant numérique.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Le nom du groupe.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>La description, lorsqu'elle est présente.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Soit <c>Vsts</c>, soit <c>AzureKeyVault</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Si le groupe est partagé avec d'autres projets.</summary>
    [JsonPropertyName("isShared")]
    public bool IsShared { get; set; }

    /// <summary>Quand le serveur a enregistré un changement pour la dernière fois.</summary>
    [JsonPropertyName("modifiedOn")]
    public DateTimeOffset? ModifiedOn { get; set; }

    /// <summary>Les variables, indexées par nom.</summary>
    [JsonPropertyName("variables")]
    public IDictionary<string, VariableDto>? Variables { get; set; }

    // Noter ce qui est absent : variableGroupProjectReferences. Ce champ est bien transmis, mais
    // volontairement non mappé, car il ne sert qu'à mettre à jour un groupe, ce que cet outil ne fait jamais.
}
