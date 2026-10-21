namespace DevToolbox.Tools.ApiDelta.Core.Configuration;

/// <summary>Le budget qu'une description d'origine inconnue ne peut pas dépasser.</summary>
/// <param name="MaxBytes">La taille maximale du fichier.</param>
/// <param name="MaxDepth">La profondeur d'imbrication maximale.</param>
/// <param name="MaxReferences">Le nombre maximal de références.</param>
/// <param name="MaxAliasExpansions">
/// Le nombre maximal d'expansions d'alias. Un alias se développe, et un document qui s'en sert en cascade
/// peut peser à la lecture bien plus que sur le disque.
/// </param>
/// <remarks>
/// Ces limites existent parce que les deux entrées sont des fichiers que l'outil n'a pas écrits. Atteindre
/// une limite n'est pas une panne : c'est un refus, et le rapport nomme la limite atteinte.
/// </remarks>
public sealed record LoadLimits(
    long MaxBytes = 33_554_432,
    int MaxDepth = 200,
    int MaxReferences = 20_000,
    int MaxAliasExpansions = 10_000)
{
    /// <summary>Les limites par défaut.</summary>
    public static LoadLimits Default { get; } = new();
}
