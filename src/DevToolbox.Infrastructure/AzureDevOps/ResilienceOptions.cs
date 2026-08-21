using System.ComponentModel.DataAnnotations;

namespace DevToolbox.Infrastructure.AzureDevOps;

/// <summary>
/// Les réglages du pipeline de résilience, liés à la section de configuration <c>Resilience</c>. Déclarés
/// une seule fois par client au moment de l'enregistrement, plutôt que dispersés sur les sites d'appel.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>La section de configuration à laquelle ce type se lie.</summary>
    public const string SectionName = "Resilience";

    /// <summary>Le budget total d'une requête logique, reprises comprises.</summary>
    [Range(1, 600)]
    public int TotalRequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Le budget d'une tentative isolée.</summary>
    [Range(1, 600)]
    public int AttemptTimeoutSeconds { get; set; } = 10;

    /// <summary>Combien de fois une tentative est rejouée avant que le pipeline renonce.</summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Le délai de base entre deux reprises, en millisecondes. Le pipeline le fait croître de façon
    /// exponentielle et y ajoute une part d'aléa : c'est donc un point de départ et non une attente fixe.
    /// </summary>
    [Range(1, 60_000)]
    public int RetryBaseDelayMilliseconds { get; set; } = 1_000;

    /// <summary>La fenêtre sur laquelle le disjoncteur échantillonne les échecs.</summary>
    [Range(1, 600)]
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>La proportion d'échecs, dans la fenêtre, qui ouvre le disjoncteur.</summary>
    [Range(0.01, 1.0)]
    public double CircuitBreakerFailureRatio { get; set; } = 0.2;

    /// <summary>Le nombre minimal d'appels dans la fenêtre avant que le disjoncteur puisse s'ouvrir.</summary>
    [Range(1, 1000)]
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;
}
