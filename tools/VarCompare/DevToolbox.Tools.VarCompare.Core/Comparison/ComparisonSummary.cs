namespace DevToolbox.Tools.VarCompare.Core.Comparison;

/// <summary>
/// Les décomptes affichés avec une comparaison, pour que le développeur constate que rien n'a été omis en
/// silence.
/// </summary>
/// <param name="TotalVariables">Noms de variables distincts, tous groupes sélectionnés confondus.</param>
/// <param name="PresentEverywhere">Variables présentes dans tous les groupes.</param>
/// <param name="MissingSomewhere">Variables absentes d'au moins un groupe.</param>
/// <param name="DifferingValues">Variables présentes partout dont les valeurs lisibles diffèrent.</param>
/// <param name="SecretCells">Cellules portant un secret dont la valeur ne peut pas être lue.</param>
/// <param name="KeyVaultSourcedCells">Cellules appartenant à un groupe adossé à un coffre de clés.</param>
/// <param name="ReadOnlyCells">Cellules marquées en lecture seule dans leur groupe.</param>
/// <param name="UndeterminedCells">Cellules dont l'état n'a pas pu être déterminé.</param>
public sealed record ComparisonSummary(
    int TotalVariables,
    int PresentEverywhere,
    int MissingSomewhere,
    int DifferingValues,
    int SecretCells,
    int KeyVaultSourcedCells,
    int ReadOnlyCells,
    int UndeterminedCells);
