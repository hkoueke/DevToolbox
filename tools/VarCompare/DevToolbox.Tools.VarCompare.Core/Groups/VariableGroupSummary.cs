namespace DevToolbox.Tools.VarCompare.Core.Groups;

/// <summary>Un groupe de variables tel qu'il apparaît dans la liste, avant lecture de ses variables.</summary>
/// <param name="Id">L'identifiant numérique.</param>
/// <param name="Name">Le nom du groupe.</param>
/// <param name="Description">La description, lorsque le groupe en a une.</param>
/// <param name="Origin">Si le groupe est ordinaire ou adossé à un coffre de clés.</param>
/// <param name="IsShared">Si le groupe est partagé avec d'autres projets.</param>
public sealed record VariableGroupSummary(
    int Id,
    string Name,
    string? Description,
    VariableGroupOrigin Origin,
    bool IsShared)
{
    /// <summary>Indique si toutes les variables de ce groupe proviennent d'un coffre de clés.</summary>
    public bool IsKeyVaultBacked => Origin == VariableGroupOrigin.KeyVaultBacked;
}
