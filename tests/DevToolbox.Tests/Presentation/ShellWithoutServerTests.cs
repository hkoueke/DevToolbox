using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Runs;
using DevToolbox.Presentation.Shell;
using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// La coquille doit pouvoir servir un outil qui ne s'adresse à personne. apidelta lit deux fichiers
/// locaux : exiger une adresse de serveur pour atteindre son onglet serait exiger d'en inventer une.
/// </summary>
public sealed class ShellWithoutServerTests
{
    [Fact]
    public void A_tool_that_needs_no_server_says_so_and_the_others_have_nothing_to_declare()
    {
        // Le membre par défaut est le mécanisme : seul l'outil hors ligne le redéfinit, et aucun outil
        // existant n'a été modifié pour que celui-ci existe. La question se pose à travers l'interface,
        // car c'est là que vit la valeur par défaut — et c'est aussi par là que la coquille interroge.
        ITool serverBacked = new ServerBackedTool();
        ITool offline = new OfflineTool();

        serverBacked.RequiresServer.Should().BeTrue();
        offline.RequiresServer.Should().BeFalse();
    }

    [Fact]
    public void The_header_names_no_target_when_none_is_configured()
    {
        TestConsole console = new();
        ShellLayout layout = new(console);

        layout.RenderHeader([new OfflineTool()], activeTab: null, target: null);

        console.Output.Should().NotContain("▸");
    }

    [Fact]
    public void The_header_names_the_target_when_one_is_configured()
    {
        TestConsole console = new();
        ShellLayout layout = new(console);

        layout.RenderHeader(
            [new ServerBackedTool()], activeTab: null, target: "devops.entreprise.local ▸ DefaultCollection");

        console.Output.Should().Contain("devops.entreprise.local");
        console.Output.Should().Contain("DefaultCollection");
    }

    [Fact]
    public void The_offline_tools_tab_appears_beside_the_others()
    {
        TestConsole console = new();
        ShellLayout layout = new(console);
        IReadOnlyList<ITool> tools = [new ServerBackedTool(), new OfflineTool()];

        layout.RenderHeader(tools, activeTab: null, target: null);

        ShellLayout.DistinctTabs(tools).Should().Equal("Azure DevOps", "OpenAPI");
        console.Output.Should().Contain("OpenAPI");
    }

    private sealed class ServerBackedTool : ITool
    {
        public string Id => "varcompare";

        public string DisplayName => "varcompare";

        public string Tab => "Azure DevOps";

        public string Description => "Compare des groupes de variables.";

        public Task<ToolRun> RunAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Cet outil factice n'est jamais exécuté.");
    }

    private sealed class OfflineTool : ITool
    {
        public string Id => "apidelta";

        public string DisplayName => "apidelta";

        public string Tab => "OpenAPI";

        public string Description => "Compare deux descriptions OpenAPI locales.";

        public bool RequiresServer => false;

        public Task<ToolRun> RunAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Cet outil factice n'est jamais exécuté.");
    }
}
