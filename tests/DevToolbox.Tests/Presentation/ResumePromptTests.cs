using DevToolbox.Application.Abstractions;
using DevToolbox.Presentation.Shell;
using FluentAssertions;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// Les trois réponses offertes à une exécution interrompue font-elles trois choses différentes ?
/// </summary>
/// <remarks>
/// Seule « Reprendre » était couverte, et elle l'était de biais — par un essai de reprise qui pousse la
/// touche Entrée sur le premier choix. Les deux autres entrées n'étaient éprouvées par rien, et leurs
/// libellés se terminaient tous deux par « repartir de zéro » sans dire ce qu'elles faisaient de la
/// séance mémorisée : elles passaient donc pour un doublon, ce qui a été signalé. La différence est
/// réelle, et c'est ici qu'elle se tient.
/// </remarks>
public sealed class ResumePromptTests
{
    [Fact]
    public void No_candidate_means_a_fresh_start_without_a_question()
    {
        TestConsole console = new();

        ResumeAnswer answer = new ResumePrompt(console).Ask([]);

        answer.Decision.Should().Be(ResumeDecision.StartFresh);
        answer.Run.Should().BeNull();
        console.Output.Should().BeEmpty("rien à reprendre ne se demande pas");
    }

    [Theory]
    [InlineData(0, ResumeDecision.Resume)]
    [InlineData(1, ResumeDecision.Discard)]
    [InlineData(2, ResumeDecision.StartFresh)]
    public void Each_of_the_three_entries_yields_its_own_decision(int down, ResumeDecision expected)
    {
        TestConsole console = Ready(down);

        ResumeAnswer answer = new ResumePrompt(console).Ask([Interrupted()]);

        answer.Decision.Should().Be(expected);
        answer.Run.Should().NotBeNull("la séance concernée doit accompagner la réponse");
    }

    [Fact]
    public void The_three_labels_differ_and_each_says_what_becomes_of_the_session()
    {
        TestConsole console = Ready(0);

        _ = new ResumePrompt(console).Ask([Interrupted()]);

        string output = console.Output;

        output.Should().Contain("Reprendre là où vous en étiez");
        output.Should().Contain("oublier cette séance", "la suppression doit être dite");
        output.Should().Contain(
            "redemander au prochain lancement",
            "la seule différence entre les deux départs de zéro doit figurer dans le libellé");
    }

    [Fact]
    public void The_panel_says_what_was_remembered_and_how_far_it_went()
    {
        TestConsole console = Ready(0);

        _ = new ResumePrompt(console).Ask([Interrupted()]);

        console.Output.Should().Contain("apidelta");
        console.Output.Should().Contain("production.yaml", "la sélection mémorisée doit être montrée");
        console.Output.Should().Contain("Lecture de votre production", "l'étape atteinte doit être dite");
    }

    /// <summary>Une console prête à répondre : « down » fois la flèche, puis Entrée.</summary>
    private static TestConsole Ready(int down)
    {
        TestConsole console = new();
        console.Profile.Width = 200;
        console.Interactive();

        for (int step = 0; step < down; step++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);

        return console;
    }

    private static PersistedRun Interrupted() => new(
        Guid.NewGuid(),
        "apidelta",
        [new PersistedSelectionEntry("fichier de production", "production.yaml", "production.yaml")],
        ["Lecture de votre production"],
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);
}
