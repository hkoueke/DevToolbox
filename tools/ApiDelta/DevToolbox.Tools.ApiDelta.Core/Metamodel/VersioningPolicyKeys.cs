namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Les cas où l'effet d'un changement relève d'une politique et non de la spécification.
/// </summary>
/// <remarks>
/// Nommés ici parce que la table les désigne et que le moteur de version les résout : deux chaînes
/// littérales qui doivent correspondre sont une politique silencieusement ignorée.
/// </remarks>
public static class VersioningPolicyKeys
{
    /// <summary>Une valeur d'énumération ajoutée.</summary>
    public const string EnumValueAdded = "enumValueAdded";

    /// <summary>Une réponse ajoutée à une opération.</summary>
    public const string ResponseAdded = "responseAdded";
}
