namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Les identifiants stables des diagnostics de configuration.</summary>
/// <remarks>
/// Distincts de ceux de la lecture : une configuration invalide n'est pas une description illisible, et
/// un lecteur qui voit <c>CONFIG-INVALID</c> sait tout de suite lequel de ses deux fichiers regarder.
/// </remarks>
public static class ConfigurationCodes
{
    /// <summary>La configuration ne peut pas être appliquée telle qu'elle est écrite.</summary>
    public const string Invalid = "CONFIG-INVALID";

    /// <summary>Une sourdine a cessé de valoir.</summary>
    public const string SuppressionExpired = "CONFIG-SUPPRESSION-EXPIRED";

    /// <summary>Une sourdine n'apparie plus rien.</summary>
    public const string SuppressionUnmatched = "CONFIG-SUPPRESSION-UNMATCHED";
}
