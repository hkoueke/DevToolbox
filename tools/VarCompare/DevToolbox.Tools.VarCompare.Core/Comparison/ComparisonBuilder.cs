using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>
/// Construit une comparaison à partir des clichés d'au moins deux groupes.
/// </summary>
/// <remarks>
/// Pure : ni entrées-sorties, ni horloge, ni console. Tout ce dont elle a besoin arrive en argument, ce qui
/// rend les règles de comparaison directement testables.
/// </remarks>
public static class ComparisonBuilder
{
    /// <summary>Construit la comparaison sur l'union des noms de variables de tous les clichés.</summary>
    /// <param name="snapshots">Les groupes à comparer, dans l'ordre de sélection.</param>
    /// <returns>La comparaison.</returns>
    /// <exception cref="ArgumentException">Moins de deux clichés ont été fournis.</exception>
    public static VariableComparison Build(IReadOnlyList<VariableGroupSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count < VariableComparison.MinimumGroups)
        {
            throw new ArgumentException(
                "A comparison needs at least two groups; there is no useful single-group mode.",
                nameof(snapshots));
        }

        IReadOnlyList<ComparisonRow> rows = BuildRows(snapshots);

        return new VariableComparison(
            [.. snapshots.Select(snapshot => snapshot.Summary)],
            rows,
            snapshots.Min(snapshot => snapshot.RetrievedAt));
    }

    /// <summary>Déduit l'état d'une variable dans un groupe.</summary>
    /// <param name="snapshot">Le groupe.</param>
    /// <param name="entry">La variable dans ce groupe, ou <see langword="null"/> si elle est absente.</param>
    /// <returns>Exactement un état.</returns>
    public static CellState DeriveState(VariableGroupSnapshot snapshot, VariableEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // L'état dégradé est vérifié AVANT l'absence, et cet ordre est tout l'enjeu : lorsqu'un groupe n'a
        // pas pu être lu, nous ignorons si une variable en est absente. Annoncer « absente » serait une
        // affirmation que rien n'étaye.
        if (snapshot.IsDegraded)
        {
            return CellState.Undetermined;
        }

        if (entry is null)
        {
            return CellState.Absent;
        }

        if (entry.IsSecret)
        {
            return CellState.PresentSecret;
        }

        if (entry.Value is null)
        {
            // Présente, non marquée secrète, et pourtant illisible : le serveur a renvoyé quelque chose que
            // nous ne savons pas interpréter comme une valeur. La déclarer inconnue est honnête, la déclarer
            // vide ne le serait pas.
            return CellState.Undetermined;
        }

        return entry.Value.Length == 0 ? CellState.PresentEmpty : CellState.PresentWithValue;
    }

    private static IReadOnlyList<ComparisonRow> BuildRows(IReadOnlyList<VariableGroupSnapshot> snapshots)
    {
        // L'union des noms, rapprochés sans tenir compte de la casse, comme le fait la plateforme.
        HashSet<string> canonical = new(StringComparer.OrdinalIgnoreCase);

        foreach (VariableGroupSnapshot snapshot in snapshots)
        {
            foreach (string name in snapshot.Names)
            {
                canonical.Add(name);
            }
        }

        return
        [
            .. canonical
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => BuildRow(name, snapshots)),
        ];
    }

    private static ComparisonRow BuildRow(string canonicalName, IReadOnlyList<VariableGroupSnapshot> snapshots)
    {
        List<ComparisonCell> cells = new(snapshots.Count);
        List<string> spellings = [];

        foreach (VariableGroupSnapshot snapshot in snapshots)
        {
            VariableEntry? entry = snapshot.Find(canonicalName);

            if (entry is not null)
            {
                spellings.Add(entry.Name);
            }

            cells.Add(new ComparisonCell(
                DeriveState(snapshot, entry),
                snapshot.Summary.IsKeyVaultBacked,
                entry?.IsReadOnly ?? false,
                entry?.Name));
        }

        return new ComparisonRow(
            canonicalName.ToUpperInvariant(),
            spellings.Count > 0 ? spellings[0] : canonicalName,
            cells,
            HasCasingDivergence(spellings),
            ReadableValuesDiffer(canonicalName, snapshots));
    }

    private static bool HasCasingDivergence(IReadOnlyList<string> spellings) =>
        spellings.Distinct(StringComparer.Ordinal).Count() > 1;

    /// <summary>
    /// Indique si la variable est présente partout mais que ses valeurs lisibles ne sont pas identiques.
    /// </summary>
    /// <remarks>
    /// Seules des valeurs lisibles peuvent être comparées. Si un groupe détient la variable comme secret ou
    /// comme valeur de coffre de clés, il n'y a rien à confronter, et la réponse est « pas connu comme
    /// différent » plutôt qu'une supposition.
    /// </remarks>
    private static bool ReadableValuesDiffer(
        string canonicalName,
        IReadOnlyList<VariableGroupSnapshot> snapshots)
    {
        List<string> readable = [];

        foreach (VariableGroupSnapshot snapshot in snapshots)
        {
            VariableEntry? entry = snapshot.Find(canonicalName);

            if (entry is null || snapshot.IsDegraded || entry.IsSecret || entry.Value is null)
            {
                return false;
            }

            readable.Add(entry.Value);
        }

        return readable.Distinct(StringComparer.Ordinal).Count() > 1;
    }
}
