using DevToolbox.Tools.VarCompare.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Déduction de la collection et du projet à partir d'un dépôt distant git.
/// </summary>
public sealed class WorkingFolderInspectorTests
{
    [Theory]
    [InlineData(
        "https://devops.entreprise.local",
        "https://devops.entreprise.local/DefaultCollection/MyProject/_git/MyRepo",
        "DefaultCollection", "MyProject", "MyRepo")]
    [InlineData(
        "https://devops.entreprise.local",
        "https://user@devops.entreprise.local/DefaultCollection/MyProject/_git/MyRepo",
        "DefaultCollection", "MyProject", "MyRepo")]
    [InlineData(
        "https://devops.entreprise.local:8080/tfs",
        "https://devops.entreprise.local:8080/tfs/DefaultCollection/MyProject/_git/MyRepo",
        "DefaultCollection", "MyProject", "MyRepo")]
    [InlineData(
        "ssh://devops.entreprise.local:22",
        "ssh://devops.entreprise.local:22/DefaultCollection/MyProject/_git/MyRepo",
        "DefaultCollection", "MyProject", "MyRepo")]
    [InlineData(
        "https://devops.entreprise.local",
        "https://devops.entreprise.local/DefaultCollection/My%20Project/_git/MyRepo",
        "DefaultCollection", "My Project", "MyRepo")]
    public void A_recognised_remote_yields_its_collection_project_and_repository(
        string baseUrl,
        string remoteUrl,
        string expectedCollection,
        string expectedProject,
        string expectedRepository)
    {
        (string Collection, string Project, string Repository)? derived =
            WorkingFolderInspector.DeriveFromUrl(remoteUrl, new Uri(baseUrl));

        derived.Should().NotBeNull();
        derived.Value.Collection.Should().Be(expectedCollection);
        derived.Value.Project.Should().Be(expectedProject);
        derived.Value.Repository.Should().Be(expectedRepository);
    }

    [Fact]
    public void A_virtual_directory_is_not_mistaken_for_the_collection()
    {
        // Le piège : /tfs fait partie de l'URL de base, pas de la collection.
        (string Collection, string Project, string Repository)? derived =
            WorkingFolderInspector.DeriveFromUrl(
                "https://devops.entreprise.local/tfs/DefaultCollection/MyProject/_git/MyRepo",
                new Uri("https://devops.entreprise.local/tfs"));

        derived.Should().NotBeNull();
        derived.Value.Collection.Should().Be("DefaultCollection");
        derived.Value.Collection.Should().NotBe("tfs");
    }

    [Theory]
    [InlineData("https://devops.entreprise.local/DefaultCollection/MyProject")]
    [InlineData("https://devops.entreprise.local/_git/MyRepo")]
    [InlineData("not-a-url")]
    [InlineData("https://devops.entreprise.local/")]
    public void An_unrecognisable_remote_yields_nothing_rather_than_a_guess(string remoteUrl)
    {
        WorkingFolderInspector
            .DeriveFromUrl(remoteUrl, new Uri("https://devops.entreprise.local"))
            .Should().BeNull();
    }

    [Fact]
    public void Remotes_are_parsed_from_the_git_config_ini()
    {
        string[] config =
        [
            "[core]",
            "\trepositoryformatversion = 0",
            "[remote \"origin\"]",
            "\turl = https://devops.entreprise.local/DefaultCollection/MyProject/_git/MyRepo",
            "\tfetch = +refs/heads/*:refs/remotes/origin/*",
            "[remote \"upstream\"]",
            "\turl = https://github.com/example/other.git",
            "[branch \"main\"]",
            "\tremote = origin",
        ];

        IReadOnlyList<(string Remote, string Url)> remotes = WorkingFolderInspector.ParseRemotes(config);

        remotes.Should().HaveCount(2);
        remotes[0].Remote.Should().Be("origin");
        remotes[0].Url.Should().Be("https://devops.entreprise.local/DefaultCollection/MyProject/_git/MyRepo");
        remotes[1].Remote.Should().Be("upstream");
    }

    [Fact]
    public void A_config_with_no_remote_yields_none()
    {
        string[] config = ["[core]", "\tbare = false"];

        WorkingFolderInspector.ParseRemotes(config).Should().BeEmpty();
    }

    [Fact]
    public void A_branch_section_is_not_mistaken_for_a_remote()
    {
        // Une section de branche possède une clé « remote » sans être pour autant la définition d'un dépôt
        // distant.
        string[] config = ["[branch \"main\"]", "\tremote = origin", "\tmerge = refs/heads/main"];

        WorkingFolderInspector.ParseRemotes(config).Should().BeEmpty();
    }
}
