namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>Une variable, vue à travers tous les groupes sélectionnés.</summary>
/// <param name="CanonicalName">La clé de rapprochement, insensible à la casse.</param>
/// <param name="DisplayName">La casse du premier groupe qui possède la variable.</param>
/// <param name="Cells">Une cellule par groupe, dans le même ordre et en même nombre que la liste de groupes.</param>
/// <param name="HasCasingDivergence">Si les groupes orthographient le nom différemment.</param>
/// <param name="ReadableValuesDiffer">
/// Si la variable est présente partout mais que ses valeurs lisibles ne sont pas identiques.
/// </param>
public sealed record ComparisonRow(
    string CanonicalName,
    string DisplayName,
    IReadOnlyList<ComparisonCell> Cells,
    bool HasCasingDivergence,
    bool ReadableValuesDiffer)
{
    /// <summary>Indique si la variable est présente dans tous les groupes sélectionnés.</summary>
    public bool IsPresentEverywhere => Cells.All(cell => cell.IsPresent);

    /// <summary>Indique si la variable manque dans au moins un groupe.</summary>
    public bool IsMissingSomewhere => Cells.Any(cell => !cell.IsPresent);

    /// <summary>
    /// Indique si la ligne est identique dans tous les groupes, et donc masquée par le filtre des seules
    /// différences.
    /// </summary>
    public bool IsIdenticalEverywhere =>
        IsPresentEverywhere && !ReadableValuesDiffer && !HasCasingDivergence && SameStateEverywhere();

    private bool SameStateEverywhere() =>
        Cells.Count == 0 || Cells.All(cell => cell.State == Cells[0].State);
}
