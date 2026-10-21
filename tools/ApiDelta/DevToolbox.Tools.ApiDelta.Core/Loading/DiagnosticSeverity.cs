namespace DevToolbox.Tools.ApiDelta.Core.Loading;

/// <summary>La gravité d'un diagnostic de lecture.</summary>
public enum DiagnosticSeverity
{
    /// <summary>La description ne peut pas être lue fidèlement : rien ne sera publié.</summary>
    Error,

    /// <summary>Quelque chose mérite d'être dit sans empêcher la lecture.</summary>
    Warning,

    /// <summary>Une observation.</summary>
    Information,
}
