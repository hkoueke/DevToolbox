using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>
/// Le résultat de la comparaison d'au moins deux groupes de variables : une ligne par nom distinct, une
/// cellule par groupe.
/// </summary>
public sealed class VariableComparison
{
    /// <summary>Le nombre minimal de groupes à partir duquel une comparaison a un sens.</summary>
    public const int MinimumGroups = 2;

    /// <summary>Crée une comparaison.</summary>
    /// <param name="groups">Les groupes retenus, dans l'ordre de sélection, qui fixe l'ordre des colonnes.</param>
    /// <param name="rows">Les lignes, triées par nom canonique.</param>
    /// <param name="retrievedAt">L'horodatage du cliché le plus ancien, celui que l'affichage annonce.</param>
    public VariableComparison(
        IReadOnlyList<VariableGroupSummary> groups,
        IReadOnlyList<ComparisonRow> rows,
        DateTimeOffset retrievedAt)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(rows);

        Groups = groups;
        Rows = rows;
        RetrievedAt = retrievedAt;

        IReadOnlyList<ComparisonCell> cells = [.. rows.SelectMany(row => row.Cells)];

        Summary = new ComparisonSummary(
            rows.Count,
            rows.Count(row => row.IsPresentEverywhere),
            rows.Count(row => row.IsMissingSomewhere),
            rows.Count(row => row.ReadableValuesDiffer),
            cells.Count(cell => cell.State == CellState.PresentSecret),
            cells.Count(cell => cell.IsKeyVaultSourced && cell.IsPresent),
            cells.Count(cell => cell.IsReadOnly),
            cells.Count(cell => cell.State == CellState.Undetermined));
    }

    /// <summary>Les groupes comparés. L'ordre des colonnes est celui de la sélection.</summary>
    public IReadOnlyList<VariableGroupSummary> Groups { get; }

    /// <summary>Une ligne par nom de variable distinct, triées de façon prévisible.</summary>
    public IReadOnlyList<ComparisonRow> Rows { get; }

    /// <summary>Les décomptes annoncés avec la comparaison.</summary>
    public ComparisonSummary Summary { get; }

    /// <summary>
    /// Quand le plus ancien des groupes comparés a été lu. La comparaison reflète cet instant précis, et
    /// c'est le rafraîchissement qui prend en compte les modifications ultérieures.
    /// </summary>
    public DateTimeOffset RetrievedAt { get; }

    /// <summary>Les lignes restantes après application du filtre des seules différences.</summary>
    /// <param name="differencesOnly">S'il faut masquer les lignes identiques dans tous les groupes.</param>
    /// <returns>Les lignes visibles.</returns>
    public IReadOnlyList<ComparisonRow> VisibleRows(bool differencesOnly) =>
        differencesOnly
            ? [.. Rows.Where(row => !row.IsIdenticalEverywhere)]
            : Rows;

    /// <summary>Combien de lignes le filtre masque, ce que l'affichage doit annoncer.</summary>
    /// <param name="differencesOnly">Si le filtre est actif.</param>
    /// <returns>Le nombre de lignes masquées.</returns>
    public int HiddenRowCount(bool differencesOnly) =>
        differencesOnly ? Rows.Count - VisibleRows(true).Count : 0;
}
