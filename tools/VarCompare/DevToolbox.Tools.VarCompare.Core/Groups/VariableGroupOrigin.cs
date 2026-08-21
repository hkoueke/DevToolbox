namespace DevToolbox.Tools.VarCompare.Core.Groups;

/// <summary>
/// D'où proviennent les valeurs d'un groupe de variables. Le rattachement à un coffre de clés est une
/// propriété du groupe, pas d'une variable prise isolément.
/// </summary>
public enum VariableGroupOrigin
{
    /// <summary>Un groupe ordinaire dont les valeurs vivent dans Azure DevOps. Valeur transmise <c>Vsts</c>.</summary>
    Ordinary = 0,

    /// <summary>Un groupe adossé à un coffre de clés. Valeur transmise <c>AzureKeyVault</c>.</summary>
    KeyVaultBacked,
}
