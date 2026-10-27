using System.Text.Json;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Presentation;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta;

/// <summary>
/// Ce qu'une exécution apidelta laisse derrière elle et, bien plus important, ce qu'elle n'y laisse pas.
/// </summary>
public sealed class ApiDeltaCheckpointShapeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Nothing_is_worth_resuming_before_a_selection_exists()
    {
        ToolRunContext context = CreateContext();

        Factory().TryCreate(context).Should().BeNull(
            "tant que rien n'est choisi, il n'y a rien à reprendre");
    }

    [Fact]
    public void The_serialized_checkpoint_carries_the_selection_and_nothing_else()
    {
        ToolRunContext context = CreateContext();
        context.Set(ApiDeltaContextKeys.Selection, Selection());

        PersistedRun? checkpoint = Factory().TryCreate(context);

        string json = JsonSerializer.Serialize(checkpoint, SerializerOptions);

        using JsonDocument document = JsonDocument.Parse(json);

        document.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "runId", "toolId", "selection", "completedSteps", "startedAt", "lastUpdatedAt");

        // Les chemins et les empreintes sont là, parce que c'est ce qui rend la reprise vérifiable.
        json.Should().Contain("production.yaml");
        json.Should().Contain("sha256:3f9a7c21b0d4");

        // Et rien du contenu des descriptions : il n'existe aucun chemin d'une sélection vers un nœud.
        json.Should().NotContain("components");
        json.Should().NotContain("openapi");
    }

    [Fact]
    public void A_selection_survives_a_round_trip_through_the_checkpoint()
    {
        ToolRunContext context = CreateContext();
        context.Set(ApiDeltaContextKeys.Selection, Selection());

        PersistedRun checkpoint = Factory().TryCreate(context)!;

        RunSelection? read = ApiDeltaCheckpointFactory.TryRead(checkpoint);

        read.Should().NotBeNull();
        read.Mode.Should().Be(RunMode.Consolidate);
        read.ProductionPath.Should().EndWith("production.yaml");
        read.CandidatePath.Should().EndWith("new-api.json");
        read.OutputDirectory.Should().Be(Path.Combine("C:", "travail", "artefacts"));

        // L'empreinte revient abrégée, ce qui suffit : elle sert à confronter, pas à prouver.
        read.ProductionHash.Should().Be("3f9a7c21b0d4");
    }

    [Fact]
    public void A_checkpoint_this_version_cannot_read_disables_resuming_rather_than_failing()
    {
        // Un point de reprise écrit par une version antérieure, ou tronqué, ne doit pas empêcher de
        // travailler : il rend seulement la reprise indisponible.
        PersistedRun foreign = new(
            Guid.NewGuid(),
            "apidelta",
            [new PersistedSelectionEntry("quelque-chose", "d'autre", "d'autre")],
            [],
            Now,
            Now);

        ApiDeltaCheckpointFactory.TryRead(foreign).Should().BeNull();
    }

    private static ApiDeltaCheckpointFactory Factory()
    {
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        return new ApiDeltaCheckpointFactory(clock);
    }

    private static ToolRunContext CreateContext() =>
        new(new ToolRun(Guid.NewGuid(), "apidelta", Now, [ApiDeltaStepNames.Select]));

    private static RunSelection Selection() =>
        new(
            RunMode.Consolidate,
            Path.Combine("C:", "travail", "production.yaml"),
            "3f9a7c21b0d4e8f1a2b3c4d5e6f7a8b9",
            Path.Combine("C:", "travail", "artefacts"),
            CandidatePath: Path.Combine("C:", "travail", "new-api.json"),
            CandidateHash: "7c21f0a4e8b21234567890abcdef1234");
}
