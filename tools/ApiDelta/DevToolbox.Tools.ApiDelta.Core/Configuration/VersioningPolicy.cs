namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Les choix de versionnage qui relèvent d'une politique et non de la spécification.</summary>
/// <param name="EnumValueAdded">
/// Ce que vaut une valeur d'énumération ajoutée. Majeur par défaut : un client engendré qui traite
/// exhaustivement une énumération cesse de compiler quand elle s'allonge.
/// </param>
/// <param name="SuccessResponseAdded">Ce que vaut une réponse de succès ajoutée.</param>
/// <param name="ErrorResponseAdded">Ce que vaut une réponse d'erreur ajoutée.</param>
/// <param name="FallbackBaseline">
/// La version de repli lorsque celle de la production est illisible. Nulle par défaut : une version ne
/// s'invente pas, elle se signale.
/// </param>
public sealed record VersioningPolicy(
    VersionImpact EnumValueAdded = VersionImpact.Major,
    VersionImpact SuccessResponseAdded = VersionImpact.Minor,
    VersionImpact ErrorResponseAdded = VersionImpact.Patch,
    string? FallbackBaseline = null)
{
    /// <summary>La politique par défaut.</summary>
    public static VersioningPolicy Default { get; } = new();
}
