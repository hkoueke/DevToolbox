using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Domain.Runs;
using DevToolbox.Infrastructure.Platform;
using DevToolbox.Infrastructure.Storage;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Steps;
using DevToolbox.Tools.ApiDelta.Presentation;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Core.Steps;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

public sealed class ResumeActionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "resume-actions-" + Guid.NewGuid().ToString("N"));
    private readonly FileRunCheckpointStore _store;

    public ResumeActionTests()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        _store = new FileRunCheckpointStore(new AppPaths(_root, _root), clock, NullLogger<FileRunCheckpointStore>.Instance);
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData("apidelta", 0)]
    [InlineData("apidelta", 1)]
    [InlineData("apidelta", 2)]
    [InlineData("varcompare", 0)]
    [InlineData("varcompare", 1)]
    [InlineData("varcompare", 2)]
    public async Task Startup_choices_have_the_announced_effects_on_both_tools(string tool, int choice)
    {
        string file = Path.Combine(_root, "reference.yaml");
        await File.WriteAllTextAsync(file, "source must remain");
        PersistedRun old = Saved(tool, file);
        await _store.SaveAsync(old, CancellationToken.None);
        using TestConsole console = Ready(choice);
        ToolRunContext context = Context(tool);
        bool resumed = await Resume(tool, context, console);
        resumed.Should().Be(choice == 0);
        context.Has(string.Equals(tool, "apidelta", StringComparison.Ordinal) ? ApiDeltaContextKeys.ResumedSelection : VarCompareContextKeys.ResumedSelection)
            .Should().Be(choice == 0);
        var available = await _store.ListResumableAsync(tool, CancellationToken.None);
        available.Count.Should().Be(choice == 1 ? 0 : 1);
        (await File.ReadAllTextAsync(file)).Should().Be("source must remain");
        if (choice == 2)
        {
            PersistedRun replacement = old with { RunId = Guid.NewGuid() };
            await _store.SaveAsync(replacement, CancellationToken.None);
            (await _store.ListResumableAsync(tool, CancellationToken.None)).Should().ContainSingle()
                .Which.RunId.Should().Be(replacement.RunId);
            console.Output.Should().Contain("jusqu’à l’enregistrement des nouveaux");
        }
        else
        {
            console.Output.Should().Contain(choice == 0 ? "sont repris" : "ont été oubliés");
        }
    }

    [Theory]
    [InlineData("apidelta")]
    [InlineData("varcompare")]
    public async Task Invalid_saved_choices_are_forgotten_instead_of_being_offered_on_each_opening(string tool)
    {
        await _store.SaveAsync(Saved(tool, "input.yaml") with { Selection = [] }, CancellationToken.None);
        using TestConsole console = Ready(0);
        ToolRunContext context = Context(tool);
        (await Resume(tool, context, console)).Should().BeFalse();
        (await _store.ListResumableAsync(tool, CancellationToken.None)).Should().BeEmpty();
        context.Has(CheckpointContextKeys.SourceRunId).Should().BeFalse();
        console.Output.Should().Contain("oubliés").And.Contain("choisissez à nouveau");
    }

    [Fact]
    public async Task Changed_ApiDelta_files_invalidate_the_session_and_explain_the_return_to_file_selection()
    {
        await _store.SaveAsync(Saved("apidelta", "input.yaml"), CancellationToken.None);
        using TestConsole console = Ready(0);
        var probe = Substitute.For<ISpecFileProbe>();
        probe.ProbeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SpecFileFacts("input.yaml", 10, "different")));
        var coordinator = new ApiDeltaResumeCoordinator(new ResumePrompt(console), _store, probe, console);
        (await coordinator.TryResumeAsync("apidelta", Context("apidelta"), CancellationToken.None)).Should().BeFalse();
        (await _store.ListResumableAsync("apidelta", CancellationToken.None)).Should().BeEmpty();
        console.Output.Should().Contain("a été modifié").And.Contain("choisissez à nouveau vos fichiers");
    }

    private Task<bool> Resume(string tool, ToolRunContext context, TestConsole console)
    {
        if (string.Equals(tool, "varcompare", StringComparison.Ordinal))
        {
            return new DevToolbox.Tools.VarCompare.Presentation.ResumeCoordinator(new ResumePrompt(console), _store, console)
                .TryResumeAsync(tool, context, CancellationToken.None);
        }

        var probe = Substitute.For<ISpecFileProbe>();
        probe.ProbeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success(new SpecFileFacts(call.Arg<string>(), 10, "hash")));
        return new ApiDeltaResumeCoordinator(new ResumePrompt(console), _store, probe, console)
            .TryResumeAsync(tool, context, CancellationToken.None);
    }

    private static PersistedRun Saved(string tool, string file) => new(Guid.NewGuid(), tool,
    [
        new PersistedSelectionEntry(ApiDeltaSelectionKinds.Mode, "Analyse", "analyse"),
        new PersistedSelectionEntry(ApiDeltaSelectionKinds.Production, file, file + " · sha256:hash"),
        new PersistedSelectionEntry(ApiDeltaSelectionKinds.Output, Path.GetDirectoryName(file) ?? ".", "results"),
        new PersistedSelectionEntry(VarCompareSelectionKinds.Project, "project", "Project"),
        new PersistedSelectionEntry(VarCompareSelectionKinds.Group, "1", "One"),
        new PersistedSelectionEntry(VarCompareSelectionKinds.Group, "2", "Two"),
    ], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static ToolRunContext Context(string tool) =>
        new(new ToolRun(Guid.NewGuid(), tool, DateTimeOffset.UnixEpoch, ["First"]));

    private static TestConsole Ready(int choice)
    {
        var console = new TestConsole().Interactive();
        console.Profile.Width = 240;
        for (int i = 0; i < choice; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
        return console;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
