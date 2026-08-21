namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>Quelle disposition de comparaison afficher.</summary>
public enum ComparisonLayout
{
    /// <summary>Mesurer le terminal et choisir, en avertissant avant de basculer vers l'affichage empilé.</summary>
    Auto = 0,

    /// <summary>Une colonne par groupe.</summary>
    SideBySide,

    /// <summary>Chaque variable listée une fois, ses états par groupe présentés en dessous.</summary>
    Stacked,
}
