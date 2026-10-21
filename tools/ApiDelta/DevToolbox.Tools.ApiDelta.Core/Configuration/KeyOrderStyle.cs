namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>L'ordre dans lequel les clés sont écrites.</summary>
public enum KeyOrderStyle
{
    /// <summary>Un ordre stable, déterminé par l'outil.</summary>
    Canonical,

    /// <summary>Celui que l'entrée employait.</summary>
    Preserve,
}
