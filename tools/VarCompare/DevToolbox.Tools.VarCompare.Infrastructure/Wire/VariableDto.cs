using System.Text.Json.Serialization;

namespace DevToolbox.Tools.VarCompare.Infrastructure.Wire;

/// <summary>Une entrée de la table <c>variables</c> telle qu'elle est transmise.</summary>
public sealed class VariableDto
{
    /// <summary>La valeur, que le serveur renvoie nulle pour un secret.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Si la valeur est détenue comme secrète.</summary>
    [JsonPropertyName("isSecret")]
    public bool IsSecret { get; set; }

    /// <summary>Si la variable est marquée en lecture seule.</summary>
    [JsonPropertyName("isReadOnly")]
    public bool IsReadOnly { get; set; }
}
