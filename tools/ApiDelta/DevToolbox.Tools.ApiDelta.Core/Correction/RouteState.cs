namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>Ce qu'une route du candidat vaut, du point de vue de la production.</summary>
/// <remarks>
/// Trois états et non deux : une route que la production porte déjà à l'identique doit pouvoir être
/// montrée comme telle. Sans cet état, le développeur qui cherche une route et ne la trouve pas ne peut
/// pas distinguer « elle correspondait déjà » de « l'outil l'a manquée », et seul l'outil sait lequel des
/// deux c'est.
/// </remarks>
public enum RouteState
{
    /// <summary>La production ne porte pas cette route.</summary>
    Absent,

    /// <summary>La production la porte, et le candidat en dit autre chose.</summary>
    Differs,

    /// <summary>La production la porte, et le candidat n'en dit rien de nouveau.</summary>
    Identical,
}
