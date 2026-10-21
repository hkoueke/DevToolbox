namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>La fin de ligne employée en sortie.</summary>
public enum LineEndingStyle
{
    /// <summary>Un saut de ligne seul.</summary>
    Lf,

    /// <summary>Un retour chariot suivi d'un saut de ligne.</summary>
    Crlf,

    /// <summary>Celle que l'entrée employait.</summary>
    Preserve,
}
