namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>Ce qu'est une collection pour OpenAPI, par opposition à ce qu'elle est pour YAML.</summary>
public enum CollectionKind
{
    /// <summary>Ce n'est pas une collection.</summary>
    None,

    /// <summary>
    /// Un ensemble : l'ordre n'a pas de sens. Deux écritures qui diffèrent par l'ordre ne diffèrent pas.
    /// </summary>
    SetLike,

    /// <summary>
    /// Une suite dont l'ordre compte, soit parce qu'OpenAPI le dit, soit parce que des outils en
    /// dépendent — les alternatives de sécurité, une composition.
    /// </summary>
    Ordered,

    /// <summary>Une association dont les clés sont des identités.</summary>
    KeyedMap,
}
