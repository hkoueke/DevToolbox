using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Core.Groups;
using DevToolbox.Tools.VarCompare.Presentation.Prompts;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// L'invite de sélection des groupes, et surtout la sortie qu'elle propose.
/// </summary>
/// <remarks>
/// Une invite à choix multiples exige au moins une case cochée : sans entrée de retour, un développeur
/// arrivé ici par erreur n'aurait que Ctrl+C pour s'en aller. Ces contrôles vérifient que la sortie existe,
/// qu'une seule touche y mène, et qu'elle se lit à part des groupes.
/// </remarks>
public sealed class GroupSelectionPromptTests
{
    [Fact]
    public async Task Checking_the_back_entry_leaves_without_comparing_anything()
    {
        using TestConsole console = NewConsole();

        // La liste boucle : une seule flèche haut mène à l'entrée de retour, qui est en fin de liste.
        console.Input.PushKey(ConsoleKey.UpArrow);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<VariableGroupSummary> chosen =
            await new GroupSelectionPrompt(console).ChooseAsync(Groups(3), CancellationToken.None);

        chosen.Should().BeEmpty("cocher le retour s'en va, sans réclamer deux groupes au passage");
    }

    [Fact]
    public async Task The_back_entry_carries_the_mark_that_tells_it_apart_from_a_group()
    {
        using TestConsole console = NewConsole();

        console.Input.PushKey(ConsoleKey.UpArrow);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        await new GroupSelectionPrompt(console).ChooseAsync(Groups(3), CancellationToken.None);

        // La flèche est la marque qui distingue une navigation d'un choix, et elle est écrite en ASCII pour
        // rester lisible sur un terminal qui ne restitue pas l'Unicode.
        console.Output.Should().Contain(NavigationLabels.Back);
        console.Output.Should().Contain("groupe-1");
    }

    [Fact]
    public async Task Two_checked_groups_are_returned_and_the_back_entry_is_not_one_of_them()
    {
        using TestConsole console = NewConsole();

        // Cocher les deux premiers groupes, puis valider.
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        IReadOnlyList<VariableGroupSummary> chosen =
            await new GroupSelectionPrompt(console).ChooseAsync(Groups(3), CancellationToken.None);

        chosen.Select(group => group.Name).Should().BeEquivalentTo("groupe-1", "groupe-2");
    }

    private static TestConsole NewConsole()
    {
        TestConsole console = new();
        console.Interactive();
        console.Profile.Width = 120;

        return console;
    }

    private static IReadOnlyList<VariableGroupSummary> Groups(int count) =>
    [
        .. Enumerable.Range(1, count).Select(index =>
            new VariableGroupSummary(index, $"groupe-{index}", null, VariableGroupOrigin.Ordinary, false)),
    ];
}
