namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>Ce qu'il est advenu d'une référence.</summary>
public enum ReferenceResolution
{
    /// <summary>Elle désigne quelque chose qui existe.</summary>
    Resolved,

    /// <summary>Elle ne désigne rien.</summary>
    Unresolved,

    /// <summary>Elle sort du document, et la politique en vigueur l'interdit.</summary>
    DisallowedByPolicy,

    /// <summary>Elle sort du document, et la politique l'autorise : elle reste telle quelle.</summary>
    PreservedExternal,
}
