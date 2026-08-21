using System.Net;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.AzureDevOps;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.AzureDevOps;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Le comportement de la passerelle face à un serveur simulé : pagination, conversion, traduction des
/// erreurs et garantie de lecture seule.
/// </summary>
public sealed class VariableGroupGatewayTests
{
    private static readonly ProjectIdentifier Project = new("MyProject");

    [Fact]
    public async Task Paging_is_followed_to_exhaustion()
    {
        // Une liste partielle est un échec, jamais un succès tronqué. Deux pages doivent rendre les deux.
        using FakeHttpMessageHandler handler = new();
        handler
            .Enqueue(HttpStatusCode.OK, GroupsPage(1, "app-dev"), continuationToken: "page-2")
            .Enqueue(HttpStatusCode.OK, GroupsPage(2, "app-prod"));

        VariableGroupGateway gateway = CreateGateway(handler);

        Result<IReadOnlyList<VariableGroupSummary>> groups =
            await gateway.ListGroupsAsync(Project, CancellationToken.None);

        groups.IsSuccess.Should().BeTrue();
        groups.Value.Select(group => group.Name).Should().Equal("app-dev", "app-prod");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].RequestUri!.Query.Should().Contain("continuationToken=page-2");
    }

    [Fact]
    public async Task Every_request_the_gateway_issues_is_a_GET()
    {
        // Vérifié sur du trafic réel plutôt que sur le code source.
        using FakeHttpMessageHandler handler = new();
        handler
            .Enqueue(HttpStatusCode.OK, GroupsPage(1, "app-dev"))
            .Enqueue(HttpStatusCode.OK, SingleGroup());

        VariableGroupGateway gateway = CreateGateway(handler);

        await gateway.ListGroupsAsync(Project, CancellationToken.None);
        await gateway.GetGroupAsync(Project, 42, CancellationToken.None);

        handler.Requests.Should().OnlyContain(request => request.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task A_group_maps_its_variables_states_and_origin()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(HttpStatusCode.OK, SingleGroup());

        VariableGroupGateway gateway = CreateGateway(handler);

        Result<VariableGroupSnapshot> snapshot =
            await gateway.GetGroupAsync(Project, 42, CancellationToken.None);

        snapshot.IsSuccess.Should().BeTrue();
        snapshot.Value.Summary.Name.Should().Be("app-prod");
        snapshot.Value.Summary.Origin.Should().Be(VariableGroupOrigin.Ordinary);
        snapshot.Value.Count.Should().Be(3);

        snapshot.Value.Find("Api__Key")!.IsSecret.Should().BeTrue();
        snapshot.Value.Find("Api__Key")!.Value.Should().BeNull();
        snapshot.Value.Find("Feature__X")!.IsReadOnly.Should().BeTrue();
        snapshot.Value.Find("Feature__X")!.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task A_key_vault_backed_group_is_recognised_from_its_type()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(HttpStatusCode.OK, SingleGroup(type: "AzureKeyVault"));

        VariableGroupGateway gateway = CreateGateway(handler);

        Result<VariableGroupSnapshot> snapshot =
            await gateway.GetGroupAsync(Project, 42, CancellationToken.None);

        snapshot.Value.Summary.IsKeyVaultBacked.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, FailureReason.AuthenticationFailed)]
    [InlineData(HttpStatusCode.Forbidden, FailureReason.PermissionDenied)]
    [InlineData(HttpStatusCode.NotFound, FailureReason.GroupNotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable, FailureReason.ServiceUnavailable)]
    [InlineData(HttpStatusCode.MovedPermanently, FailureReason.RedirectRefused)]
    public async Task Status_codes_map_to_domain_failures(HttpStatusCode status, FailureReason expected)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(status);

        VariableGroupGateway gateway = CreateGateway(handler);

        Result<VariableGroupSnapshot> snapshot =
            await gateway.GetGroupAsync(Project, 42, CancellationToken.None);

        snapshot.IsFailure.Should().BeTrue();
        snapshot.Failure!.Reason.Should().Be(expected);
    }

    [Fact]
    public async Task An_authentication_failure_names_the_server_and_never_asks_for_a_credential()
    {
        // Dire que le serveur a rejeté la session Windows, nommer ce qui a été tenté, et s'arrêter.
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(HttpStatusCode.Unauthorized);

        VariableGroupGateway gateway = CreateGateway(handler);

        Result<VariableGroupSnapshot> snapshot =
            await gateway.GetGroupAsync(Project, 42, CancellationToken.None);

        string message = snapshot.Failure!.Message;
        message.Should().Contain("devops.entreprise.local");
        message.Should().Contain("DefaultCollection");
        message.Should().NotContainAny("password", "token", "username");
    }

    [Fact]
    public async Task No_failure_message_leaks_a_status_code_or_a_response_body()
    {
        // Les corps de réponse bruts et les codes de statut n'atteignent jamais le développeur.
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(HttpStatusCode.Forbidden, "{\"message\":\"internal detail that must not leak\"}");

        VariableGroupGateway gateway = CreateGateway(handler);

        Result<VariableGroupSnapshot> snapshot =
            await gateway.GetGroupAsync(Project, 42, CancellationToken.None);

        snapshot.Failure!.Message.Should().NotContain("internal detail");
        snapshot.Failure.Message.Should().NotContain("403");
    }

    [Fact]
    public async Task A_failure_on_a_later_page_fails_the_whole_read()
    {
        // Jamais une liste silencieusement tronquée.
        using FakeHttpMessageHandler handler = new();
        handler
            .Enqueue(HttpStatusCode.OK, GroupsPage(1, "app-dev"), continuationToken: "page-2")
            .Enqueue(HttpStatusCode.Forbidden);

        VariableGroupGateway gateway = CreateGateway(handler);

        Result<IReadOnlyList<VariableGroupSummary>> groups =
            await gateway.ListGroupsAsync(Project, CancellationToken.None);

        groups.IsFailure.Should().BeTrue();
        groups.Failure!.Reason.Should().Be(FailureReason.PermissionDenied);
    }

    private static VariableGroupGateway CreateGateway(FakeHttpMessageHandler handler)
    {
        AzureDevOpsServerOptions options = new()
        {
            BaseUrl = "https://devops.entreprise.local",
            Collection = "DefaultCollection",
            ApiVersion = "7.1",
        };

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://devops.entreprise.local/"),
        };

        IOptions<AzureDevOpsServerOptions> wrapped = Options.Create(options);

        AzureDevOpsApiReader reader = new(
            new AzureDevOpsClient(httpClient),
            wrapped,
            NullLogger<AzureDevOpsApiReader>.Instance);

        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));

        return new VariableGroupGateway(reader, wrapped, clock);
    }

    private static string GroupsPage(int id, string name) =>
        $$"""
        { "count": 1, "value": [ { "id": {{id}}, "name": "{{name}}", "type": "Vsts", "isShared": false } ] }
        """;

    private static string SingleGroup(string type = "Vsts") =>
        $$"""
        {
          "id": 42,
          "name": "app-prod",
          "type": "{{type}}",
          "isShared": false,
          "modifiedOn": "2026-08-14T09:11:02.1Z",
          "variables": {
            "Api__BaseUrl": { "value": "https://api.example.com", "isSecret": false, "isReadOnly": false },
            "Api__Key":     { "value": null, "isSecret": true,  "isReadOnly": false },
            "Feature__X":   { "value": "",   "isSecret": false, "isReadOnly": true  }
          },
          "variableGroupProjectReferences": [ { "name": "ignored" } ]
        }
        """;
}
