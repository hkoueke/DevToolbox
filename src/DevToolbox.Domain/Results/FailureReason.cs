namespace DevToolbox.Domain.Results;

/// <summary>
/// Pourquoi une opération n'a pas abouti. Ce sont des issues métier, volontairement dépourvues de détail de
/// transport : aucun code de statut HTTP, aucune URL, aucun corps de réponse n'atteint jamais ce type.
/// </summary>
public enum FailureReason
{
    /// <summary>Aucun échec. Présent pour que <c>default</c> ne soit pas une raison significative.</summary>
    None = 0,

    /// <summary>Le serveur a rejeté les identifiants Windows de l'appelant.</summary>
    AuthenticationFailed,

    /// <summary>Le serveur connaît l'appelant mais ne l'autorise pas à lire la ressource.</summary>
    PermissionDenied,

    /// <summary>Un groupe de variables attendu est introuvable.</summary>
    GroupNotFound,

    /// <summary>Service indisponible : le budget de reprises est épuisé ou le disjoncteur est ouvert.</summary>
    ServiceUnavailable,

    /// <summary>Le serveur est totalement injoignable (échec DNS ou de connexion).</summary>
    ServerUnreachable,

    /// <summary>Le certificat TLS du serveur n'a pas pu être validé.</summary>
    ServerUntrusted,

    /// <summary>
    /// Le serveur a répondu par une redirection, refusée pour que les identifiants ne la suivent jamais.
    /// </summary>
    RedirectRefused,

    /// <summary>La configuration est absente ou invalide ; l'exécution ne peut pas démarrer.</summary>
    InvalidConfiguration,

    /// <summary>
    /// Une entrée désignée par le développeur est inutilisable : fichier absent, illisible, trop volumineux,
    /// ou sélection vide là où il en faut une.
    /// </summary>
    /// <remarks>
    /// Distinguée de <see cref="InvalidConfiguration"/> : celle-ci porte sur les réglages de la boîte à
    /// outils, celle-là sur ce que le développeur vient de désigner. Les deux se corrigent à des endroits
    /// différents, et le message doit pouvoir le dire.
    /// </remarks>
    InvalidInput,

    /// <summary>L'appelant a annulé l'opération.</summary>
    Cancelled,
}
