using System.ComponentModel.DataAnnotations;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Là où la boîte à outils est pointée. Lié à la section de configuration <c>AzureDevOpsServer</c> et validé
/// au démarrage. Partagé par tous les outils, pour que le second en hérite.
/// </summary>
public sealed class AzureDevOpsServerOptions
{
    /// <summary>La section de configuration à laquelle ce type se lie.</summary>
    public const string SectionName = "AzureDevOpsServer";

    /// <summary>L'URL de base absolue du serveur sur site, sans le segment de collection.</summary>
    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>La collection, par exemple <c>DefaultCollection</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(64, MinimumLength = 1)]
    public string Collection { get; set; } = string.Empty;

    /// <summary>La version de l'API REST envoyée à chaque requête.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ApiVersion { get; set; } = "7.1";

    /// <summary>
    /// Autorise une URL de base en HTTP simple. Nommée de façon à ne pas pouvoir être activée par
    /// inadvertance, et fausse par défaut. Il n'existe volontairement aucune option pour désactiver la
    /// validation des certificats.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>
    /// Le délai d'attente par requête, en secondes. C'est le filet extérieur, et il doit rester
    /// franchement au-dessus de <c>Resilience:TotalRequestTimeoutSeconds</c> : il couvre aussi les attentes
    /// entre reprises, si bien que deux échéances trop proches courent l'une contre l'autre. Le validateur
    /// le vérifie.
    /// </summary>
    [Range(1, 600)]
    public int HttpTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Combien de groupes peuvent être lus simultanément. Borné et configurable, jamais illimité.
    /// </summary>
    [Range(1, 32)]
    public int MaxDegreeOfParallelism { get; set; } = 5;
}
