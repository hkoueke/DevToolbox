using DevToolbox.Infrastructure.AzureDevOps;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Une configuration que DevToolbox accepte doit être une configuration que le pipeline accepte.
/// </summary>
/// <remarks>
/// Le gestionnaire standard impose des rapports entre réglages, et il ne les vérifie qu'à la première
/// création du client typé — donc en pleine exécution, sous forme d'exception. Les bornes déclarées par
/// <see cref="ResilienceOptions"/> laissaient passer des combinaisons qu'il refuse ; ces tests ferment
/// l'écart, et la vérification finale le prouve en construisant réellement le client.
/// </remarks>
public sealed class ResilienceConfigurationTests
{
    [Fact]
    public void The_shipped_settings_actually_build_a_client()
    {
        // La preuve qui compte : ce que le dépôt livre passe la validation de bout en bout.
        using ServiceProvider provider =
            BuildProvider(new Dictionary<string, string?>(StringComparer.Ordinal));

        Action act = () => provider.GetRequiredService<AzureDevOpsClient>();

        act.Should().NotThrow();
    }

    [Fact]
    public void Zero_retries_is_refused_at_startup_rather_than_at_the_first_request()
    {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Resilience:MaxRetryAttempts"] = "0",
            });

        Action act = () => _ = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*MaxRetryAttempts*");
    }

    [Fact]
    public void A_sampling_window_too_short_for_the_attempt_timeout_is_refused()
    {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Resilience:AttemptTimeoutSeconds"] = "20",
                ["Resilience:CircuitBreakerSamplingDurationSeconds"] = "30",
            });

        Action act = () => _ = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*CircuitBreakerSamplingDurationSeconds*");
    }

    [Fact]
    public void A_total_budget_shorter_than_one_attempt_is_refused()
    {
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Resilience:AttemptTimeoutSeconds"] = "20",
                ["Resilience:TotalRequestTimeoutSeconds"] = "10",
                ["Resilience:CircuitBreakerSamplingDurationSeconds"] = "40",
            });

        Action act = () => _ = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*TotalRequestTimeoutSeconds*");
    }

    [Fact]
    public void A_total_budget_equal_to_one_attempt_is_accepted_because_the_pipeline_accepts_it()
    {
        // La frontière a été mesurée contre le pipeline réel, et non déduite de son message d'erreur, qui
        // annonce « greater » : total 9 / tentative 10 est refusé, total 10 / tentative 10 est accepté.
        // Resserrer ce test en « strictement supérieur » rendrait DevToolbox plus sévère que ce qu'il
        // enveloppe, et refuserait une configuration qui fonctionne.
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Resilience:AttemptTimeoutSeconds"] = "10",
                ["Resilience:TotalRequestTimeoutSeconds"] = "10",
                ["Resilience:CircuitBreakerSamplingDurationSeconds"] = "20",
            });

        Action act = () =>
        {
            _ = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            _ = provider.GetRequiredService<AzureDevOpsClient>();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void A_sampling_window_of_exactly_twice_the_attempt_timeout_is_accepted()
    {
        // Même méthode : le pipeline refuse 19 s pour une tentative de 10 s et accepte 20 s.
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Resilience:AttemptTimeoutSeconds"] = "10",
                ["Resilience:CircuitBreakerSamplingDurationSeconds"] = "20",
            });

        Action act = () =>
        {
            _ = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
            _ = provider.GetRequiredService<AzureDevOpsClient>();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void A_client_timeout_that_does_not_exceed_the_pipeline_budget_is_refused()
    {
        // Les deux échéances expireraient ensemble, et laquelle l'emporte cesserait d'être décidable :
        // tantôt un échec propre, tantôt une exception qui traverse toutes les couches.
        using ServiceProvider provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["AzureDevOpsServer:HttpTimeoutSeconds"] = "30",
                ["Resilience:TotalRequestTimeoutSeconds"] = "30",
            });

        Action act = () => _ = provider.GetRequiredService<IOptions<AzureDevOpsServerOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*HttpTimeoutSeconds*");
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> overrides)
    {
        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            ["AzureDevOpsServer:BaseUrl"] = "https://devops.entreprise.local",
            ["AzureDevOpsServer:Collection"] = "DefaultCollection",
            ["AzureDevOpsServer:ApiVersion"] = "7.1",
            ["AzureDevOpsServer:HttpTimeoutSeconds"] = "120",
            ["Resilience:TotalRequestTimeoutSeconds"] = "30",
            ["Resilience:AttemptTimeoutSeconds"] = "10",
            ["Resilience:MaxRetryAttempts"] = "3",
            ["Resilience:RetryBaseDelayMilliseconds"] = "1000",
            ["Resilience:CircuitBreakerSamplingDurationSeconds"] = "30",
            ["Resilience:CircuitBreakerFailureRatio"] = "0.2",
            ["Resilience:CircuitBreakerMinimumThroughput"] = "5",
        };

        foreach (KeyValuePair<string, string?> entry in overrides)
        {
            settings[entry.Key] = entry.Value;
        }

        ConfigurationManager configuration = new();
        configuration.AddInMemoryCollection(settings);

        ServiceCollection services = [];
        services.AddLogging(builder => builder.ClearProviders());
        services.AddAzureDevOps(configuration, "appsettings.json");

        return services.BuildServiceProvider();
    }
}
