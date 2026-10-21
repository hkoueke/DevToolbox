namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>La sorte d'un nœud de l'arbre de syntaxe.</summary>
public enum NodeKind
{
    /// <summary>Une association de clés à des nœuds.</summary>
    Mapping,

    /// <summary>Une suite ordonnée de nœuds.</summary>
    Sequence,

    /// <summary>Une valeur terminale.</summary>
    Scalar,
}
