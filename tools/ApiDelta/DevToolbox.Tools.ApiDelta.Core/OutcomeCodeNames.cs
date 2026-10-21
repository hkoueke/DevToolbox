namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Le nom publié de chaque issue.</summary>
/// <remarks>
/// Fixé ici et non dérivé du nom du membre : le vocabulaire est un contrat de sortie, et une mise en
/// minuscules mécanique écrirait <c>outputfailure</c> là où le contrat annonce <c>outputFailure</c>.
/// </remarks>
public static class OutcomeCodeNames
{
    /// <summary>Le nom publié d'une issue.</summary>
    /// <param name="code">L'issue.</param>
    /// <returns>Le nom, tel qu'il apparaît dans les artefacts et à l'écran.</returns>
    public static string Of(OutcomeCode code) => code switch
    {
        OutcomeCode.Clean => "clean",
        OutcomeCode.ThresholdExceeded => "thresholdExceeded",
        OutcomeCode.BreakingChange => "breakingChange",
        OutcomeCode.InvalidUsage => "invalidUsage",
        OutcomeCode.LoadFailure => "loadFailure",
        OutcomeCode.OutputFailure => "outputFailure",
        _ => "internalError",
    };
}
