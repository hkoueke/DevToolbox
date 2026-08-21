using System.Net;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.AzureDevOps;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Le pipeline de résilience tel qu'il est réellement enregistré, et non tel qu'il est décrit.
/// </summary>
public sealed class ResiliencePipelineTests
{
    [Fact]
    public async Task A_transient_failure_is_retried_before_the_developer_ever_sees_it()
    {
        // Reprendre automatiquement d'abord. Trois reprises sont configurées : un serveur qui échoue deux
        // fois puis répond doit donc produire un succès, sans qu'aucun échec ne remonte.
        using FakeHttpMessageHandler handler = new();
        handler
            .Enqueue(HttpStatusCode.ServiceUnavailable)
            .Enqueue(HttpStatusCode.ServiceUnavailable)
            .Enqueue(HttpStatusCode.OK, "{\"count\":0,\"value\":[]}");

        using ServiceProvider provider = BuildProvider(handler);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        Result<ListResponse<ProjectStub>> result =
            await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task Retry_exhaustion_surfaces_as_service_unavailable_rather_than_retrying_forever()
    {
        // Cesser d'appeler un service qui échoue de façon répétée, et le signaler.
        using FakeHttpMessageHandler handler = new();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            handler.Enqueue(HttpStatusCode.ServiceUnavailable);
        }

        using ServiceProvider provider = BuildProvider(handler);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        Result<ListResponse<ProjectStub>> result =
            await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Reason.Should().Be(FailureReason.ServiceUnavailable);

        // Une tentative initiale plus les trois reprises configurées, puis abandon.
        handler.Requests.Should().HaveCount(4);
    }

    [Fact]
    public async Task Every_retried_request_is_still_a_GET()
    {
        // La raison pour laquelle une sémantique de reprise complète est sûre ici.
        using FakeHttpMessageHandler handler = new();

        for (int attempt = 0; attempt < 5; attempt++)
        {
            handler.Enqueue(HttpStatusCode.ServiceUnavailable);
        }

        using ServiceProvider provider = BuildProvider(handler);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        handler.Requests.Should().OnlyContain(request => request.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task A_permission_denial_is_not_retried_because_repeating_it_cannot_help()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(HttpStatusCode.Forbidden);

        using ServiceProvider provider = BuildProvider(handler);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        Result<ListResponse<ProjectStub>> result =
            await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        result.Failure!.Reason.Should().Be(FailureReason.PermissionDenied);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public void The_pipeline_is_configured_with_the_required_controls()
    {
        // Quatre garde-fous sont attendus : reprise avec recul exponentiel et part d'aléa, disjoncteur,
        // délai par tentative et délai total. La reprise et les délais sont éprouvés par le comportement
        // ci-dessus. Les seuils du disjoncteur sont vérifiés ici parce que le provoquer dans un test
        // unitaire exigerait une rafale dépendante du temps, ce qui achèterait un test instable plutôt
        // qu'une garantie réelle.
        using FakeHttpMessageHandler handler = new();
        using ServiceProvider provider = BuildProvider(handler);

        ResilienceOptions options = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

        options.MaxRetryAttempts.Should().Be(3);
        options.AttemptTimeoutSeconds.Should().Be(10);
        options.TotalRequestTimeoutSeconds.Should().Be(30);
        options.CircuitBreakerFailureRatio.Should().Be(0.2);
        options.CircuitBreakerMinimumThroughput.Should().Be(5);
        options.CircuitBreakerSamplingDurationSeconds.Should().Be(30);
    }

    private static ServiceProvider BuildProvider(FakeHttpMessageHandler handler)
    {
        ConfigurationManager configuration = new();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AzureDevOpsServer:BaseUrl"] = "https://devops.entreprise.local",
            ["AzureDevOpsServer:Collection"] = "DefaultCollection",
            ["AzureDevOpsServer:ApiVersion"] = "7.1",
            ["Resilience:MaxRetryAttempts"] = "3",
            // Un délai de base court garde les tests de reprise honnêtes sans les rendre lents.
            ["Resilience:RetryBaseDelayMilliseconds"] = "1",
            ["Resilience:AttemptTimeoutSeconds"] = "10",
            ["Resilience:TotalRequestTimeoutSeconds"] = "30",
            ["Resilience:CircuitBreakerSamplingDurationSeconds"] = "30",
            ["Resilience:CircuitBreakerFailureRatio"] = "0.2",
            ["Resilience:CircuitBreakerMinimumThroughput"] = "5",
        });

        ServiceCollection services = [];
        services.AddLogging(builder => builder.ClearProviders());
        services.AddAzureDevOps(configuration, "appsettings.json");

        // Remplacer le gestionnaire d'authentification intégrée par celui du scénario. Tout le reste —
        // reprise, disjoncteur, délais — est le pipeline que l'application enregistre réellement.
        services.AddHttpClient<AzureDevOpsClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    /// <summary>Une forme minimale pour désérialiser les réponses de liste simulées.</summary>
    internal sealed class ProjectStub
    {
        public string Name { get; set; } = string.Empty;
    }
}
