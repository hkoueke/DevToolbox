using DevToolbox.Tools.VarCompare.Core.Groups;

namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>
/// Le détail d'une variable groupe par groupe, construit à la demande lorsque le développeur l'ouvre.
/// </summary>
/// <remarks>
/// <para>
/// Ce type existe séparément de <see cref="ComparisonRow"/> pour une seule raison : la ligne ne porte
/// volontairement aucune valeur, si bien que la comparaison ne peut pas en laisser fuir. Un détail n'est
/// construit que sur action explicite, à partir de clichés gardés en mémoire, et il n'est jamais journalisé
/// ni conservé sur disque.
/// </para>
/// </remarks>
public sealed class VariableDetail
{
    private VariableDetail(string displayName, IReadOnlyList<VariableDetailEntry> perGroup)
    {
        DisplayName = displayName;
        PerGroup = perGroup;
    }

    /// <summary>Le nom de la variable, tel que l'orthographie le premier groupe qui la possède.</summary>
    public string DisplayName { get; }

    /// <summary>L'état et la valeur lisible de la variable dans chaque groupe, dans l'ordre des colonnes.</summary>
    public IReadOnlyList<VariableDetailEntry> PerGroup { get; }

    /// <summary>Construit le détail d'une variable à partir des clichés gardés en mémoire.</summary>
    /// <param name="snapshots">Les groupes comparés, dans l'ordre des colonnes.</param>
    /// <param name="canonicalName">La variable à décrire, rapprochée sans tenir compte de la casse.</param>
    /// <returns>Le détail.</returns>
    public static VariableDetail Build(
        IReadOnlyList<VariableGroupSnapshot> snapshots,
        string canonicalName)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);

        List<VariableDetailEntry> entries = new(snapshots.Count);
        string displayName = canonicalName;
        bool named = false;

        foreach (VariableGroupSnapshot snapshot in snapshots)
        {
            VariableEntry? entry = snapshot.Find(canonicalName);
            CellState state = ComparisonBuilder.DeriveState(snapshot, entry);

            if (entry is not null && !named)
            {
                displayName = entry.Name;
                named = true;
            }

            entries.Add(new VariableDetailEntry(
                snapshot.Summary.Name,
                state,
                ReadableValue(state, entry),
                entry?.Name,
                entry?.IsReadOnly ?? false,
                snapshot.Summary.IsKeyVaultBacked));
        }

        return new VariableDetail(displayName, entries);
    }

    /// <summary>
    /// La valeur à montrer, nulle sauf si elle est réellement lisible.
    /// </summary>
    /// <remarks>
    /// Le filtre porte sur l'état déduit et non sur l'entrée brute : ainsi un secret, une entrée de coffre de
    /// clés et une lecture dégradée sont écartés par la même règle, au lieu de l'être par trois contrôles
    /// distincts susceptibles de diverger avec le temps.
    /// </remarks>
    private static string? ReadableValue(CellState state, VariableEntry? entry) =>
        state is CellState.PresentWithValue or CellState.PresentEmpty ? entry?.Value : null;
}
