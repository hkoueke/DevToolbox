namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>Ce qu'un groupe donné dit d'une variable, dans la vue de détail.</summary>
/// <param name="GroupName">Le groupe auquel cette entrée appartient.</param>
/// <param name="State">L'état de la variable dans ce groupe.</param>
/// <param name="Value">
/// La valeur lisible, ou <see langword="null"/> quand il n'y a rien de lisible à montrer. Une variable
/// secrète ou adossée à un coffre de clés est toujours nulle ici : la vue de détail indique qu'une valeur
/// existe et ne peut pas être lue, sans jamais afficher un substitut qu'on pourrait prendre pour le contenu.
/// </param>
/// <param name="NameAsStored">La casse employée par ce groupe, ou <see langword="null"/> si absente.</param>
/// <param name="IsReadOnly">Si la variable est en lecture seule dans ce groupe.</param>
/// <param name="IsKeyVaultSourced">Si le groupe est adossé à un coffre de clés.</param>
public sealed record VariableDetailEntry(
    string GroupName,
    CellState State,
    string? Value,
    string? NameAsStored,
    bool IsReadOnly,
    bool IsKeyVaultSourced)
{
    /// <summary>Indique qu'une valeur existe mais ne peut pas être récupérée.</summary>
    public bool ExistsButUnreadable =>
        State is CellState.PresentSecret or CellState.Undetermined
        || (State != CellState.Absent && Value is null);
}
