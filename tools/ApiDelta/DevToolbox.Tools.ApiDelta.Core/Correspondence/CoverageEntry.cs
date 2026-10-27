namespace DevToolbox.Tools.ApiDelta.Core.Correspondence;

/// <summary>L'état de couverture d'une opération.</summary>
/// <param name="Operation">Son nom.</param>
/// <param name="State">Son état, un et un seul.</param>
/// <param name="Route">La route confirmée, le cas échéant.</param>
/// <param name="Remedy">Ce qu'il faudrait faire, pour un manque ou une ambiguïté.</param>
/// <remarks>
/// Le remède est porté plutôt que laissé à déduire. Un rapport qui dirait « archiverDocument : absente »
/// serait exact et inutilisable ; « la production devrait porter DELETE /documents, que l'écran de
/// sélection permet d'insérer » est la même information et un point de départ.
/// </remarks>
public sealed record CoverageEntry(
    string Operation, CoverageState State, string? Route, string? Remedy);
