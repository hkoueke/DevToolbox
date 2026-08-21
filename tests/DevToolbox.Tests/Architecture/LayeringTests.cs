using System.Reflection;
using DevToolbox.Application.Abstractions;
using DevToolbox.Domain.Results;
using DevToolbox.Infrastructure.AzureDevOps;
using DevToolbox.Presentation.Shell;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace DevToolbox.Tests.Architecture;

/// <summary>
/// Fait respecter mécaniquement la règle des dépendances dirigées vers l'intérieur.
/// </summary>
/// <remarks>
/// Une règle de couches qu'aucun test ne vérifie dégénère en simple convention de nommage. Ces tests sont
/// cette vérification. La liste d'API bannies couvre les deux règles portant sur les sites d'appel
/// (System.Console et new HttpClient()) qu'un test de dépendances entre types ne saurait exprimer.
/// </remarks>
public sealed class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ITool).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AzureDevOpsClient).Assembly;
    private static readonly Assembly PresentationAssembly = typeof(ShellLayout).Assembly;

    [Fact]
    public void Domain_has_no_package_references()
    {
        // C'est la liberté qui justifie d'avoir le domaine dans un assemblage séparé : il ne référence rien
        // d'autre que le framework, si bien qu'aucune préoccupation d'entrée-sortie ne peut l'atteindre.
        IEnumerable<string> referenced = DomainAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !IsFrameworkAssembly(name));

        referenced.Should().BeEmpty(
            "le domaine doit n'avoir aucune référence de paquet");
    }

    [Fact]
    public void Domain_does_not_depend_on_outer_layers()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DevToolbox.Application",
                "DevToolbox.Infrastructure",
                "DevToolbox.Presentation",
                "Spectre.Console",
                "System.Net.Http")
            .GetResult();

        AssertSatisfied(result);
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Presentation()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DevToolbox.Infrastructure",
                "DevToolbox.Presentation",
                "Spectre.Console",
                "System.Net.Http")
            .GetResult();

        AssertSatisfied(result);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Presentation()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("DevToolbox.Presentation", "Spectre.Console")
            .GetResult();

        AssertSatisfied(result);
    }

    [Fact]
    public void Presentation_does_not_depend_on_Infrastructure()
    {
        // Le shell s'adresse aux ports, jamais aux adaptateurs. Seule la racine de composition connaît les deux.
        TestResult result = Types.InAssembly(PresentationAssembly)
            .ShouldNot()
            .HaveDependencyOn("DevToolbox.Infrastructure")
            .GetResult();

        AssertSatisfied(result);
    }

    [Fact]
    public void HttpClient_is_confined_to_Infrastructure()
    {
        foreach (Assembly assembly in new[] { DomainAssembly, ApplicationAssembly, PresentationAssembly })
        {
            TestResult result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("System.Net.Http")
                .GetResult();

            AssertSatisfied(result);
        }
    }

    private static void AssertSatisfied(TestResult result)
    {
        string offenders = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);

        result.IsSuccessful.Should().BeTrue(
            "la règle des dépendances vers l'intérieur est violée par : {0}", offenders);
    }

    private static bool IsFrameworkAssembly(string name) =>
        name.StartsWith("System", StringComparison.Ordinal)
        || name.Equals("mscorlib", StringComparison.Ordinal)
        || name.Equals("netstandard", StringComparison.Ordinal);
}
