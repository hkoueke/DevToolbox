namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>L'état d'une variable dans un groupe.</summary>
/// <param name="State">Exactement un état, jamais davantage.</param>
/// <param name="IsKeyVaultSourced">Si le groupe propriétaire est adossé à un coffre de clés.</param>
/// <param name="IsReadOnly">Si la variable est en lecture seule dans ce groupe.</param>
/// <param name="NameAsStored">La casse employée par ce groupe, ou <see langword="null"/> si absente.</param>
/// <remarks>
/// La cellule ne porte volontairement aucune valeur. C'est ce qui rend structurelle l'absence de valeurs
/// dans la comparaison : le moteur de rendu n'a rien à afficher même s'il le voulait, et les valeurs ne sont
/// lues depuis le cliché que lorsqu'une vue de détail est explicitement ouverte.
/// </remarks>
public sealed record ComparisonCell(
    CellState State,
    bool IsKeyVaultSourced,
    bool IsReadOnly,
    string? NameAsStored)
{
    /// <summary>Indique si la variable est présente dans ce groupe.</summary>
    public bool IsPresent => State != CellState.Absent;
}
