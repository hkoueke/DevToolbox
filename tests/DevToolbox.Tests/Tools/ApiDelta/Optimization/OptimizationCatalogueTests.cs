using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Infrastructure;
using DevToolbox.Tools.ApiDelta.Presentation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Optimization;

/// <summary>
/// Compare les règles d'optimisation câblées à celles que la spécification annonce.
/// </summary>
/// <remarks>
/// La spécification est lue sur le disque. Une règle écrite mais jamais câblée ferait écarter à
/// l'exécution une transformation que le plan propose, et le développeur lirait une proposition que rien
/// ne sait appliquer — avec une raison exacte mais inutile.
/// </remarks>
public sealed class OptimizationCatalogueTests
{
    [Fact]
    public void Every_rule_the_specification_announces_is_wired()
    {
        List<string> announced = Announced();

        announced.Should().NotBeEmpty("la spécification doit porter le tableau des règles");

        Wired().Select(rule => rule.RuleId).OrderBy(id => id, StringComparer.Ordinal)
            .Should().Equal(announced);
    }

    [Fact]
    public void The_two_rules_that_change_what_the_description_publishes_are_inactive_by_default()
    {
        IReadOnlyList<IOptimizationRule> wired = Wired();

        wired.Where(rule => !rule.EnabledByDefault).Select(rule => rule.RuleId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Should().Equal("OPT-006", "OPT-009");
    }

    [Fact]
    public void Exactly_one_rule_declares_that_it_removes()
    {
        // La protection des extensions n'a pas le même critère selon qu'on déplace ou qu'on supprime.
        // Si une deuxième règle se mettait à supprimer, elle devrait le déclarer ici.
        Wired().Where(rule => rule.Removes).Select(rule => rule.RuleId)
            .Should().Equal("OPT-007");
    }

    [Fact]
    public void No_rule_claims_an_identifier_twice()
    {
        Wired().Select(rule => rule.RuleId).Should().OnlyHaveUniqueItems();
    }

    /// <summary>Les règles telles que la racine de composition les enregistre.</summary>
    private static IReadOnlyList<IOptimizationRule> Wired()
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddSingleton(ApiDeltaOptions.Default);
        services.AddApiDeltaInfrastructure();
        services.AddApiDelta();

        using ServiceProvider provider = services.BuildServiceProvider();

        return [.. provider.GetServices<IOptimizationRule>()];
    }

    /// <summary>
    /// Les règles que le tableau de la spécification annonce, hors de celles qui sont des contraintes.
    /// </summary>
    /// <remarks>
    /// OPT-001 est l'émission canonique, qui n'est pas une transformation mais la façon d'écrire. OPT-010
    /// à OPT-012 sont des contraintes au-dessus des autres règles, et le tableau le dit lui-même : leur
    /// colonne de sûreté porte « constraint on every other rule ». Aucune des quatre n'est une règle que
    /// l'applicateur appelle.
    /// </remarks>
    private static List<string> Announced()
    {
        List<string> announced = [];

        foreach (string line in File.ReadLines(Specification()))
        {
            if (!line.StartsWith("| OPT-", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = line.Split('|', StringSplitOptions.TrimEntries);

            if (cells.Length < 5
                || cells[4].Contains("constraint on every other rule", StringComparison.Ordinal)
                || string.Equals(cells[1], "OPT-001", StringComparison.Ordinal))
            {
                continue;
            }

            announced.Add(cells[1]);
        }

        announced.Sort(StringComparer.Ordinal);

        return announced;
    }

    /// <summary>Le fichier de spécification, trouvé en remontant jusqu'à la solution.</summary>
    private static string Specification()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DevToolbox.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("l'essai doit tourner dans l'arborescence du dépôt");

        return Path.Combine(directory.FullName, "specs", "003-openapi-delta-audit", "spec.md");
    }
}
