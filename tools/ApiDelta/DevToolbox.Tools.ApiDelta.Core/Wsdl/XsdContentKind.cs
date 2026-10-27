namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>La façon dont un type complexe compose son contenu.</summary>
/// <remarks>
/// Trois formes, et la projection ne les traite pas pareil. L'ordre d'une séquence fait partie du
/// contrat ; celui d'un <c>all</c> n'existe pas, ce qui le fait correspondre exactement aux membres d'un
/// objet JSON ; un <c>choice</c> est exclusif par construction là où <c>oneOf</c> l'est par validation,
/// ce qui ne se porte qu'approximativement.
/// </remarks>
public enum XsdContentKind
{
    /// <summary>Une suite ordonnée.</summary>
    Sequence,

    /// <summary>Une suite dont l'ordre est libre.</summary>
    All,

    /// <summary>Un choix exclusif.</summary>
    Choice,

    /// <summary>Un type simple, sans contenu composé.</summary>
    Simple,

    /// <summary>Un contenu mixte : du texte et des éléments entremêlés.</summary>
    Mixed,
}
