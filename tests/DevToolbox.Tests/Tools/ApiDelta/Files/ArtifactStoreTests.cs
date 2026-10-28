using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Files;

/// <summary>
/// Une confirmation fait-elle ce qu'elle annonce, et ne le demande-t-elle qu'une fois ?
/// </summary>
/// <remarks>
/// La question « le créer et y écrire les rapports ? » était posée, puis le dossier n'était pas créé :
/// la publication du premier artefact le trouvait absent et reposait la même question. Un « oui » qui ne
/// change rien et une question qui revient apprennent la même chose à celui qui l'a donné — que son
/// accord n'a pas été entendu.
/// </remarks>
public sealed class ArtifactStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-artefacts-" + Guid.NewGuid().ToString("N"));

    private readonly IApiDeltaPrompt _prompt = Substitute.For<IApiDeltaPrompt>();

    [Fact]
    public async Task Creating_the_output_directory_is_confirmed_once_and_then_done()
    {
        string output = Path.Combine(_root, "artefacts");

        _prompt.ConfirmCreateDirectory(Arg.Any<string>()).Returns(true);

        ArtifactStore store = new(_prompt, NullLogger<ArtifactStore>.Instance);
        RunSelection selection = Selection(output);

        Result prepared = await store.PrepareAsync(
            ["rapport.md"], selection, CancellationToken.None);

        prepared.IsSuccess.Should().BeTrue(prepared.Failure?.Message);

        Directory.Exists(output).Should().BeTrue(
            "la question promet de créer le dossier : elle doit le créer");

        Result<Artifact> written = await store.PublishAsync(
            ArtifactKind.DeltaReport,
            "rapport.md",
            new ReadOnlyMemory<byte>([.. "bonjour"u8]),
            selection,
            CancellationToken.None);

        written.IsSuccess.Should().BeTrue(written.Failure?.Message);

        _prompt.Received(1).ConfirmCreateDirectory(Arg.Any<string>());
    }

    [Fact]
    public async Task A_refused_directory_is_not_created()
    {
        string output = Path.Combine(_root, "refuse");

        _prompt.ConfirmCreateDirectory(Arg.Any<string>()).Returns(false);

        Result prepared = await new ArtifactStore(_prompt, NullLogger<ArtifactStore>.Instance)
            .PrepareAsync(["rapport.md"], Selection(output), CancellationToken.None);

        prepared.IsSuccess.Should().BeFalse();
        prepared.Failure!.Reason.Should().Be(FailureReason.Cancelled);

        Directory.Exists(output).Should().BeFalse("un refus ne laisse rien derrière lui");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RunSelection Selection(string output) =>
        new(RunMode.Analyse, "production.yaml", string.Empty, output);
}
