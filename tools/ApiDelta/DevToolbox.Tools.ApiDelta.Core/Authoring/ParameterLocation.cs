namespace DevToolbox.Tools.ApiDelta.Core.Authoring;

/// <summary>Où voyage un champ de l'entrée d'une opération, une fois la route composée.</summary>
/// <remarks>
/// Rien dans un contrat de service ne dit laquelle de ces destinations convient : une opération SOAP n'a
/// qu'une entrée, un corps. C'est pourquoi la destination est demandée et non déduite.
/// <para>
/// <see cref="Nowhere" /> est un membre et non une absence. Un champ écarté est une décision inscrite au
/// registre ; un champ absent du registre serait indiscernable d'un champ sur lequel personne n'a été
/// interrogé. La différence compte le jour où quelqu'un relit le rapport pour savoir ce qu'est devenu un
/// champ qu'il cherche.
/// </para>
/// </remarks>
public enum ParameterLocation
{
    /// <summary>Un paramètre de chemin. Toujours obligatoire, OpenAPI l'exigeant.</summary>
    Path,

    /// <summary>Un paramètre de requête.</summary>
    Query,

    /// <summary>Une partie du corps, portant la référence que la projection a dérivée.</summary>
    Body,

    /// <summary>Un paramètre d'en-tête.</summary>
    Header,

    /// <summary>Écarté sciemment : consigné et rapporté, jamais omis en silence.</summary>
    Nowhere,
}
