namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>Ce qui est arrivé à un emplacement.</summary>
public enum DifferenceKind
{
    /// <summary>Il n'existait pas dans la production, et existe dans le candidat.</summary>
    Added,

    /// <summary>Il existait dans la production, et n'existe plus dans le candidat.</summary>
    Removed,

    /// <summary>Il existe des deux côtés, et sa valeur diffère.</summary>
    Modified,
}
