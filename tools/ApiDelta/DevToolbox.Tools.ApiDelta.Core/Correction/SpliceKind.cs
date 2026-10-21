namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce qu'une épissure fait à l'endroit qu'elle désigne.</summary>
public enum SpliceKind
{
    /// <summary>Remplace la valeur qui s'y trouve.</summary>
    Replace,

    /// <summary>Ajoute une entrée sous l'association qui s'y trouve.</summary>
    Add,

    /// <summary>Retire l'entrée qui s'y trouve, sa clé comprise.</summary>
    Remove,
}
