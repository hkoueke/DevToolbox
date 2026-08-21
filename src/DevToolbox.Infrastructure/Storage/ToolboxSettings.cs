namespace DevToolbox.Infrastructure.Storage;

/// <summary>
/// Préférences mémorisées d'une session à l'autre. Ne contient aucune valeur de variable ni aucun secret.
/// </summary>
public sealed class ToolboxSettings
{
    /// <summary>
    /// L'URL de base du serveur Azure DevOps confirmée au premier démarrage. La valeur présente dans
    /// appsettings.json n'est qu'une suggestion ; c'est celle-ci qui est réellement utilisée.
    /// </summary>
    public string? ServerBaseUrl { get; set; }

    /// <summary>La collection confirmée au premier démarrage.</summary>
    public string? ServerCollection { get; set; }

    /// <summary>La dernière collection utilisée, proposée par défaut la fois suivante.</summary>
    public string? LastCollection { get; set; }

    /// <summary>Le dernier projet utilisé, proposé par défaut la fois suivante.</summary>
    public string? LastProject { get; set; }

    /// <summary>La disposition de comparaison retenue en dernier par le développeur.</summary>
    public string? PreferredLayout { get; set; }
}
