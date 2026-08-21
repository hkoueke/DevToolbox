namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>
/// L'état d'une variable dans un groupe. Exactement un de ces états s'applique à chaque cellule : la
/// correspondance est une fonction totale, sans trou ni recouvrement.
/// </summary>
public enum CellState
{
    /// <summary>La variable est absente de ce groupe.</summary>
    Absent = 0,

    /// <summary>Présente, avec une valeur lisible.</summary>
    PresentWithValue,

    /// <summary>Présente, mais sa valeur est la chaîne vide.</summary>
    PresentEmpty,

    /// <summary>Présente avec une valeur secrète qui ne peut pas être relue.</summary>
    PresentSecret,

    /// <summary>Présente, mais son état n'a pas pu être déterminé : lecture dégradée ou coffre injoignable.</summary>
    Undetermined,
}
