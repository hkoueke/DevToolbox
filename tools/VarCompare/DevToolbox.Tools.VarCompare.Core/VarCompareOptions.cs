namespace DevToolbox.Tools.VarCompare.Core;

/// <summary>
/// Les réglages propres à l'outil, exprimés dans le vocabulaire du cœur afin que ni le cœur ni la
/// présentation n'aient à référencer le type d'options de la couche d'infrastructure.
/// </summary>
/// <param name="MaxDegreeOfParallelism">
/// Combien de groupes peuvent être lus simultanément. Borné et configurable, jamais illimité.
/// </param>
public sealed record VarCompareOptions(int MaxDegreeOfParallelism)
{
    /// <summary>La valeur par défaut.</summary>
    public static VarCompareOptions Default { get; } = new(5);
}
