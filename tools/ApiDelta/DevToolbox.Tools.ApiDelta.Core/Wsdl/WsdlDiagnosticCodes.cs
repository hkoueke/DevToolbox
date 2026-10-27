namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Les identifiants stables des refus propres à la lecture d'un contrat WSDL.</summary>
/// <remarks>
/// Distincts de ceux de la lecture OpenAPI, parce qu'ils nomment d'autres choses : un refus de définition
/// de type de document ou un import hors racine n'a pas d'équivalent dans un fichier JSON. Les budgets,
/// eux, réemploient le code existant : un développeur qui a appris à lire « limite atteinte » n'a pas à
/// l'apprendre deux fois.
/// </remarks>
public static class WsdlDiagnosticCodes
{
    /// <summary>Le document porte une définition de type de document.</summary>
    public const string DocumentTypeDefinition = "WSDL-DTD";

    /// <summary>La version de WSDL déclarée n'est pas prise en charge.</summary>
    public const string UnsupportedVersion = "WSDL-VERSION";

    /// <summary>Le document ne se lit pas.</summary>
    public const string ParseFailed = "WSDL-PARSE-FAILED";

    /// <summary>La structure attendue d'un <c>wsdl:definitions</c> n'est pas respectée.</summary>
    public const string StructuralViolation = "WSDL-STRUCTURE";

    /// <summary>Un import quitterait la racine à laquelle la résolution est confinée.</summary>
    public const string ImportOutsideRoot = "WSDL-IMPORT-ROOT";

    /// <summary>Un import distant, alors que la politique ne l'autorise pas.</summary>
    public const string ImportRemoteRefused = "WSDL-IMPORT-REMOTE";

    /// <summary>Un import désigne un document introuvable.</summary>
    public const string ImportUnresolved = "WSDL-IMPORT-UNRESOLVED";

    /// <summary>Deux documents déclarent le même nom dans le même espace de noms, différemment.</summary>
    public const string SchemaConflict = "WSDL-SCHEMA-CONFLICT";

    /// <summary>Le jeu de schémas ne compile pas.</summary>
    public const string SchemaInvalid = "WSDL-SCHEMA-INVALID";

    /// <summary>Un <c>xsd:redefine</c>, que l'outil refuse plutôt que de l'ignorer en silence.</summary>
    public const string Redefine = "WSDL-REDEFINE";

    /// <summary>Un cycle d'imports, légal, consigné plutôt que suivi.</summary>
    public const string ImportCycle = "WSDL-IMPORT-CYCLE";

    /// <summary>Un modèle d'adresse que l'outil n'atteint pas.</summary>
    public const string SchemeRefused = "WSDL-SCHEME";

    /// <summary>Une adresse interne qui n'a pas été confirmée en la nommant.</summary>
    public const string AddressInternal = "WSDL-ADDRESS-INTERNAL";

    /// <summary>Une adresse qui ne se résout pas.</summary>
    public const string AddressUnresolved = "WSDL-ADDRESS-UNRESOLVED";

    /// <summary>Une chaîne de redirections plus longue que son budget.</summary>
    public const string RedirectBudget = "WSDL-REDIRECTS";

    /// <summary>Une adresse derrière une authentification.</summary>
    public const string AuthenticationRequired = "WSDL-AUTH";

    /// <summary>Une récupération qui a échoué.</summary>
    public const string FetchFailed = "WSDL-FETCH-FAILED";

    /// <summary>Un schéma écrit en XSD 1.1, que l'outil ne lit pas.</summary>
    public const string Xsd11 = "WSDL-XSD11";

    /// <summary>Une réponse qui n'est pas un document XML.</summary>
    public const string ResponseNotXml = "WSDL-RESPONSE-NOT-XML";
}
