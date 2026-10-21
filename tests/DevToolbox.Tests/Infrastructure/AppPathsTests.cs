using DevToolbox.Infrastructure.Platform;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Vérifie le confinement des chemins. apidelta est le premier outil à passer une racine choisie par le
/// développeur et des noms dérivés du contenu d'un document : ce qui n'était qu'un garde-fou théorique
/// devient la frontière réelle.
/// </summary>
public sealed class AppPathsTests : IDisposable
{
    private readonly string _root;

    public AppPathsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "devtoolbox-confinement-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void A_simple_name_resolves_under_the_root()
    {
        string path = AppPaths.CombineConfined(_root, "openapi-delta-report.md");

        path.Should().StartWith(Path.GetFullPath(_root));
        Path.GetFileName(path).Should().Be("openapi-delta-report.md");
    }

    [Fact]
    public void A_nested_relative_path_resolves_under_the_root()
    {
        string path = AppPaths.CombineConfined(_root, Path.Combine("artefacts", "extrait.yaml"));

        path.Should().StartWith(Path.GetFullPath(_root));
    }

    [Fact]
    public void A_traversal_out_of_the_root_is_refused()
    {
        Action act = () => AppPaths.CombineConfined(_root, Path.Combine("..", "ailleurs.txt"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_sibling_directory_sharing_the_roots_prefix_is_refused()
    {
        // La régression que ce test fixe : la comparaison par préfixe de chaîne acceptait « C:\outside\x »
        // sous la racine « C:\out », parce que le texte de l'un commence par celui de l'autre.
        string root = Path.Combine(_root, "out");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(_root, "outside"));

        Action act = () => AppPaths.CombineConfined(root, Path.Combine("..", "outside", "x.txt"));

        act.Should().Throw<ArgumentException>(
            "« outside » n'est pas dans « out », quoi qu'en dise la comparaison de préfixes");
    }

    [Fact]
    public void An_absolute_path_is_refused()
    {
        Action act = () => AppPaths.CombineConfined(_root, Path.Combine(Path.GetTempPath(), "ailleurs.txt"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_windows_device_name_is_refused()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Windows résout « NUL » en chemin de périphérique, qui ne tombe sous aucune racine.
        Action act = () => AppPaths.CombineConfined(_root, "NUL");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_directory_link_leaving_the_root_is_refused()
    {
        string outside = Path.Combine(Path.GetTempPath(), "devtoolbox-dehors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);

        string link = Path.Combine(_root, "lien");

        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (IOException)
        {
            // Créer un lien demande un privilège que la session n'a pas forcément. Le confinement reste
            // vérifié par les autres cas ; celui-ci ne peut simplement pas être mis en place ici.
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        try
        {
            Action act = () => AppPaths.CombineConfined(_root, Path.Combine("lien", "artefact.yaml"));

            act.Should().Throw<ArgumentException>(
                "un lien déplace la destination sans changer le texte du chemin");
        }
        finally
        {
            Directory.Delete(link);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void The_root_itself_is_accepted()
    {
        string path = AppPaths.CombineConfined(_root, ".");

        path.Should().Be(Path.GetFullPath(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
