using System.Reflection;
using DevToolbox.Application.Abstractions;
using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.VarCompare.Presentation;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Presentation;

/// <summary>
/// Aucune invite de la boîte à outils ne réclame d'identifiants, et aucune ne propose de modifier quoi que
/// ce soit.
/// </summary>
/// <remarks>
/// Deux contrôles couvrent ensemble toutes les invites atteignables : la source de chaque projet d'interface
/// est passée au crible des API et des mots qui serviraient à saisir un secret, et le système de types est
/// examiné à la recherche d'un membre qui pourrait en porter un. Le second rend la garantie structurelle :
/// sans type capable de détenir un identifiant, une invite n'a rien à remplir.
/// </remarks>
public sealed class NoCredentialPromptTests
{
    private static readonly string[] UiProjectDirectories =
    [
        Path.Combine("src", "DevToolbox.Presentation"),
        Path.Combine("tools", "VarCompare", "DevToolbox.Tools.VarCompare.Presentation"),
        Path.Combine("src", "DevToolbox.Console"),
    ];

    [Fact]
    public void No_prompt_uses_the_masked_input_API_that_would_collect_a_secret()
    {
        // La méthode Secret() de Spectre est la façon dont un mot de passe serait lu. Son absence de tous les
        // projets d'interface est la preuve la plus directe qu'aucune surface de saisie de secret n'existe.
        foreach (string file in UiSourceFiles())
        {
            File.ReadAllText(file).Should().NotContain(
                ".Secret(",
                "masked input exists only to collect a secret, and this tool collects none");
        }
    }

    [Fact]
    public void No_prompt_asks_for_a_password_a_token_or_a_user_name()
    {
        string[] forbidden =
        [
            "Enter your password",
            "Enter your token",
            "Personal access token",
            "Enter your user name",
            "Enter your username",
        ];

        foreach (string file in UiSourceFiles())
        {
            string source = File.ReadAllText(file);

            foreach (string phrase in forbidden)
            {
                source.Should().NotContainEquivalentOf(phrase);
            }
        }
    }

    [Fact]
    public void No_prompt_offers_to_edit_add_or_apply_a_variable()
    {
        // L'outil ne doit présenter aucun élément suggérant qu'une valeur puisse être modifiée depuis lui.
        string session = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "tools", "VarCompare", "DevToolbox.Tools.VarCompare.Presentation",
            "ComparisonSession.cs"));

        foreach (string forbidden in new[] { "\"Edit ", "\"Add ", "\"Apply", "\"Set value", "\"Update " })
        {
            session.Should().NotContain(forbidden);
        }
    }

    [Fact]
    public void The_comparison_view_points_at_the_Library_page_instead_of_offering_to_fix_anything()
    {
        // Là où une différence apparaît, indiquer où la corriger.
        string session = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "tools", "VarCompare", "DevToolbox.Tools.VarCompare.Presentation",
            "ComparisonSession.cs"));

        session.Should().Contain("Pipelines > Library");
        session.Should().Contain("read-only");
    }

    [Fact]
    public void There_is_no_type_anywhere_that_could_carry_a_credential()
    {
        // La moitié structurelle de la garantie : sans type d'identifiant, aucune invite ne pourrait en
        // remplir un.
        Assembly[] assemblies =
        [
            typeof(ITool).Assembly,
            typeof(ShellLayout).Assembly,
            typeof(VarCompareTool).Assembly,
        ];

        IEnumerable<string> suspicious = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => type.Name)
            .Where(name =>
                name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Token", StringComparison.OrdinalIgnoreCase));

        suspicious.Should().BeEmpty(
            "authentication is the ambient Windows session; nothing may hold a credential");
    }

    [Fact]
    public void The_target_announcement_shows_where_we_are_pointed_and_no_identity()
    {
        // L'annonce porte sur le serveur et la collection. Elle ne montre volontairement aucun nom
        // d'utilisateur, car le risque d'usurpation ici est de lire le mauvais environnement, pas d'employer
        // la mauvaise identité.
        PropertyInfo[] properties = typeof(TargetAnnouncement).GetProperties();

        properties.Select(property => property.Name)
            .Should().BeEquivalentTo("ServerHost", "Collection", "AuthenticationMode");
    }

    private static IEnumerable<string> UiSourceFiles() =>
        UiProjectDirectories
            .Select(directory => Path.Combine(RepositoryRoot(), directory))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BannedSymbols.txt")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output.");
    }
}
