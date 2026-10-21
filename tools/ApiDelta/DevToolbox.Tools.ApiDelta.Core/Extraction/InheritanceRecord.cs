namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>Ce qu'une opération retenue a hérité de son contexte.</summary>
/// <param name="Selector">L'opération.</param>
/// <param name="PathParameters">Les paramètres venus du niveau du chemin, nommés.</param>
/// <param name="Servers">Les serveurs en vigueur, par leur adresse.</param>
/// <param name="Security">Les mécanismes de sécurité en vigueur, par leur clé.</param>
/// <remarks>
/// Consigné pour être affiché. Un extrait qui aurait perdu en route un paramètre de chemin ou une
/// exigence de sécurité serait indiscernable d'un extrait correct : le dire opération par opération est
/// ce qui permet à un relecteur de vérifier d'un coup d'œil ce que le fichier a emporté.
/// </remarks>
public sealed record InheritanceRecord(
    OperationSelector Selector,
    IReadOnlyList<string> PathParameters,
    IReadOnlyList<string> Servers,
    IReadOnlyList<string> Security);
