namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>Comment la comparaison doit être disposée et filtrée.</summary>
/// <param name="Layout">La disposition à employer.</param>
/// <param name="DifferencesOnly">S'il faut masquer les lignes identiques dans tous les groupes.</param>
public sealed record ComparisonViewOptions(ComparisonLayout Layout, bool DifferencesOnly)
{
    /// <summary>La vue par défaut : disposition automatique, rien de masqué.</summary>
    public static ComparisonViewOptions Default { get; } =
        new(ComparisonLayout.Auto, DifferencesOnly: false);
}
