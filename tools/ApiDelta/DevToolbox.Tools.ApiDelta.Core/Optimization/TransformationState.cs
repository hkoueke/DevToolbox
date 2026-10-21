namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Où en est une transformation.</summary>
public enum TransformationState
{
    /// <summary>Proposée, et rien de plus : le plan ne change rien.</summary>
    Proposed,

    /// <summary>Appliquée à une copie, et vérifiée.</summary>
    Applied,

    /// <summary>Écartée par une décision.</summary>
    Rejected,

    /// <summary>Empêchée : l'outil n'a pas pu établir ce qu'il lui fallait établir.</summary>
    Blocked,
}
