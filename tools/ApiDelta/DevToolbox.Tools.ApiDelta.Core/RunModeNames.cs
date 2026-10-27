namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>
/// Le nom de chaque mode, tel qu'il s'affiche.
/// </summary>
/// <remarks>
/// Nommés ici une seule fois parce qu'autrement c'est <c>ToString</c> de l'énumération qui finit à
/// l'écran, et le développeur lit alors « sans objet pour le mode everything » : un mot anglais, qui est
/// un nom de symbole et non une explication.
/// </remarks>
public static class RunModeNames
{
    /// <summary>Le nom affichable d'un mode.</summary>
    /// <param name="mode">Le mode.</param>
    /// <returns>Un nom en français, au singulier.</returns>
    public static string Of(RunMode mode) => mode switch
    {
        RunMode.Consolidate => "consolidation de la production",
        RunMode.Analyse => "analyse d'un fichier",
        _ => "inconnu",
    };
}
