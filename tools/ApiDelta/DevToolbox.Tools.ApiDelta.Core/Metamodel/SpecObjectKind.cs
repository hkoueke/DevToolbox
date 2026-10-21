namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>La sorte d'objet OpenAPI qu'un emplacement désigne.</summary>
/// <remarks>
/// En OpenAPI, c'est la position qui décide de la nature : la même clé <c>description</c> est de la
/// documentation dans un <c>info</c> et de la documentation dans une réponse, mais <c>required</c> est un
/// booléen sur un paramètre et une liste de noms sur un schéma. Le métamodèle descend donc l'arbre en
/// suivant ces sortes, plutôt que de faire correspondre des motifs de chemin — un motif ne saurait pas
/// qu'un schéma peut s'imbriquer indéfiniment en lui-même.
/// </remarks>
public enum SpecObjectKind
{
    /// <summary>La racine du document.</summary>
    Document,

    /// <summary>Le bloc <c>info</c>.</summary>
    Info,

    /// <summary>Un contact.</summary>
    Contact,

    /// <summary>Une licence.</summary>
    License,

    /// <summary>Un serveur.</summary>
    Server,

    /// <summary>Une variable de serveur.</summary>
    ServerVariable,

    /// <summary>Une étiquette.</summary>
    Tag,

    /// <summary>Une documentation externe.</summary>
    ExternalDocs,

    /// <summary>L'association des chemins.</summary>
    Paths,

    /// <summary>Un élément de chemin.</summary>
    PathItem,

    /// <summary>Une opération.</summary>
    Operation,

    /// <summary>Un paramètre.</summary>
    Parameter,

    /// <summary>Un corps de requête.</summary>
    RequestBody,

    /// <summary>L'association des réponses d'une opération.</summary>
    Responses,

    /// <summary>Une réponse.</summary>
    Response,

    /// <summary>Un type de média.</summary>
    MediaType,

    /// <summary>Un encodage.</summary>
    Encoding,

    /// <summary>Un en-tête.</summary>
    Header,

    /// <summary>Un lien.</summary>
    Link,

    /// <summary>Un rappel.</summary>
    Callback,

    /// <summary>Le bloc <c>components</c>.</summary>
    Components,

    /// <summary>Un schéma.</summary>
    Schema,

    /// <summary>Un discriminateur.</summary>
    Discriminator,

    /// <summary>Des métadonnées XML.</summary>
    Xml,

    /// <summary>Un exemple.</summary>
    Example,

    /// <summary>Un mécanisme de sécurité.</summary>
    SecurityScheme,

    /// <summary>L'ensemble des flux OAuth d'un mécanisme.</summary>
    OAuthFlows,

    /// <summary>Un flux OAuth.</summary>
    OAuthFlow,

    /// <summary>Une exigence de sécurité.</summary>
    SecurityRequirement,

    /// <summary>Une extension de fournisseur, dont l'outil ne connaît pas la grammaire.</summary>
    Extension,

    /// <summary>Un emplacement que le métamodèle ne décrit pas.</summary>
    Unknown,
}
