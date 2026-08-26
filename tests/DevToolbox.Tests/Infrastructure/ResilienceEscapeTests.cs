using System.Net;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.AzureDevOps;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Ce que le pipeline de résilience fait remonter à l'appelant lorsqu'il renonce lui-même.
/// </summary>
/// <remarks>
/// Toutes les issues ne se présentent pas sous la forme d'un <c>HttpRequestException</c>. Un budget de temps
/// épuisé lève <c>TimeoutRejectedException</c>, un disjoncteur ouvert <c>BrokenCircuitException</c> ; ni
/// l'une ni l'autre n'en dérive. Non attrapées, elles traversaient toutes les couches et terminaient le
/// processus, ce qui obligeait à tout reprendre depuis le début.
/// </remarks>
public sealed class ResilienceEscapeTests
{
    [Fact]
    public async Task An_exhausted_time_budget_comes_back_as_a_failure_rather_than_an_exception()
    {
        using SlowHandler handler = new(TimeSpan.FromSeconds(30));
        using ServiceProvider provider = BuildProvider(handler, attemptTimeoutSeconds: 1, totalSeconds: 2);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        Result<ListResponse<ProjectStub>> result =
            await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Reason.Should().Be(FailureReason.ServiceUnavailable);
        result.Failure.Message.Should().Contain("temps imparti");
    }

    [Fact]
    public async Task A_paged_read_survives_the_same_way()
    {
        using SlowHandler handler = new(TimeSpan.FromSeconds(30));
        using ServiceProvider provider = BuildProvider(handler, attemptTimeoutSeconds: 1, totalSeconds: 2);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        Result<IReadOnlyList<ProjectStub>> result = await reader.GetAllPagesAsync<ProjectStub>(
            _ => "x/_apis/projects", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Reason.Should().Be(FailureReason.ServiceUnavailable);
    }

    [Fact]
    public async Task A_client_timeout_is_told_apart_from_a_developer_cancelling()
    {
        // La distinction n'est pas cosmétique : une exécution annulée s'arrête sans rien proposer, alors
        // qu'un échec ouvre l'invite de reprise. Confondre les deux privait le développeur de tout recours.
        using SlowHandler handler = new(TimeSpan.FromSeconds(30));
        using ServiceProvider provider = BuildProvider(handler, attemptTimeoutSeconds: 1, totalSeconds: 2);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        Result<ListResponse<ProjectStub>> result =
            await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        result.Failure!.Reason.Should().NotBe(FailureReason.Cancelled);
    }

    [Fact]
    public async Task A_developer_who_actually_cancels_is_still_reported_as_a_cancellation()
    {
        using SlowHandler handler = new(TimeSpan.FromSeconds(30));
        using ServiceProvider provider = BuildProvider(handler, attemptTimeoutSeconds: 30, totalSeconds: 60);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        Result<ListResponse<ProjectStub>> result =
            await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", cancellation.Token);

        result.Failure!.Reason.Should().Be(FailureReason.Cancelled);
    }

    [Fact]
    public async Task An_open_circuit_comes_back_as_a_failure_rather_than_an_exception()
    {
        // Le disjoncteur s'ouvre pour de vrai ici. Les seuils sont resserrés afin que le test le provoque
        // sans dépendre d'un cadencement, ce qu'aucun test n'avait fait jusqu'ici.
        using FakeHttpMessageHandler handler = new();

        for (int attempt = 0; attempt < 200; attempt++)
        {
            handler.Enqueue(HttpStatusCode.InternalServerError);
        }

        using ServiceProvider provider = BuildProvider(
            handler, attemptTimeoutSeconds: 1, totalSeconds: 5, minimumThroughput: 2);

        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        Result<ListResponse<ProjectStub>> last = Result.Fail<ListResponse<ProjectStub>>(
            FailureReason.None, "jamais appelé");

        for (int call = 0; call < 6; call++)
        {
            last = await reader.GetAsync<ListResponse<ProjectStub>>(
                "x/_apis/projects", CancellationToken.None);
        }

        // Quel que soit celui des deux garde-fous qui a parlé en dernier, l'appelant reçoit un Result.
        last.IsFailure.Should().BeTrue();
        last.Failure!.Reason.Should().Be(FailureReason.ServiceUnavailable);
    }

    [Fact]
    public async Task The_developer_is_told_that_a_retry_is_under_way()
    {
        // Sans cette annonce, une reprise interne est indiscernable d'un blocage : l'écran ne bouge plus
        // pendant tout le budget de temps.
        using FakeHttpMessageHandler handler = new();
        handler
            .Enqueue(HttpStatusCode.InternalServerError)
            .Enqueue(HttpStatusCode.OK, "{\"count\":0,\"value\":[]}");

        IRetryObserver observer = Substitute.For<IRetryObserver>();

        using ServiceProvider provider = BuildProvider(handler, observer: observer);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        observer.Received(1).Retrying(1, Arg.Any<int>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task Throttling_is_announced_as_throttling_and_not_as_an_ordinary_retry()
    {
        using FakeHttpMessageHandler handler = new();
        handler
            .Enqueue(HttpStatusCode.TooManyRequests)
            .Enqueue(HttpStatusCode.OK, "{\"count\":0,\"value\":[]}");

        IRetryObserver observer = Substitute.For<IRetryObserver>();

        using ServiceProvider provider = BuildProvider(handler, observer: observer);
        AzureDevOpsApiReader reader = provider.GetRequiredService<AzureDevOpsApiReader>();

        await reader.GetAsync<ListResponse<ProjectStub>>("x/_apis/projects", CancellationToken.None);

        observer.Received(1).Throttled(Arg.Any<TimeSpan>());
        observer.DidNotReceive().Retrying(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>());
    }

    private static ServiceProvider BuildProvider(
        HttpMessageHandler handler,
        int attemptTimeoutSeconds = 10,
        int totalSeconds = 30,
        int minimumThroughput = 5,
        IRetryObserver? observer = null)
    {
        ConfigurationManager configuration = new();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AzureDevOpsServer:BaseUrl"] = "https://devops.entreprise.local",
            ["AzureDevOpsServer:Collection"] = "DefaultCollection",
            ["AzureDevOpsServer:ApiVersion"] = "7.1",
            ["AzureDevOpsServer:HttpTimeoutSeconds"] = (totalSeconds * 4).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["Resilience:MaxRetryAttempts"] = "1",
            ["Resilience:RetryBaseDelayMilliseconds"] = "1",
            ["Resilience:AttemptTimeoutSeconds"] = attemptTimeoutSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["Resilience:TotalRequestTimeoutSeconds"] = totalSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["Resilience:CircuitBreakerSamplingDurationSeconds"] = (attemptTimeoutSeconds * 2).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["Resilience:CircuitBreakerFailureRatio"] = "0.1",
            ["Resilience:CircuitBreakerMinimumThroughput"] = minimumThroughput.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
        });

        ServiceCollection services = [];
        services.AddLogging(builder => builder.ClearProviders());

        if (observer is not null)
        {
            services.AddSingleton(observer);
        }

        services.AddAzureDevOps(configuration, "appsettings.json");

        services.AddHttpClient<AzureDevOpsClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    /// <summary>Un serveur qui ne répond jamais assez vite, mais qui honore l'annulation.</summary>
    private sealed class SlowHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        internal SlowHandler(TimeSpan delay) => _delay = delay;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    /// <summary>Une forme minimale pour désérialiser les réponses de liste simulées.</summary>
    internal sealed class ProjectStub
    {
        public string Name { get; set; } = string.Empty;
    }
}
