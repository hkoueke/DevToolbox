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

    // Chargés par nom plutôt que par un type témoin : la règle doit valoir dès que l'assemblage existe,
    // sans qu'un type particulier ait à être choisi pour l'ancrer.
    private static readonly Assembly ApiDeltaCoreAssembly =
        Assembly.Load("DevToolbox.Tools.ApiDelta.Core");

    private static readonly Assembly ApiDeltaInfrastructureAssembly =
        Assembly.Load("DevToolbox.Tools.ApiDelta.Infrastructure");

    private static readonly Assembly ApiDeltaPresentationAssembly =
        Assembly.Load("DevToolbox.Tools.ApiDelta.Presentation");

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

    [Fact]
    public void ApiDelta_core_carries_neither_of_the_tool_packages()
    {
        // C'est l'assertion qui tient la décision de conception : la couche sémantique est un adaptateur,
        // et rien de ce que l'outil publie ne peut donc être sérialisé depuis le modèle d'une bibliothèque.
        // Si YamlDotNet ou Microsoft.OpenApi apparaît ici un jour, la garantie de fidélité est perdue.
        IEnumerable<string> referenced = ApiDeltaCoreAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!);

        referenced.Should().NotContain(
            ["YamlDotNet", "Microsoft.OpenApi", "Spectre.Console", "System.Net.Http"],
            "le cœur d'apidelta n'analyse pas, ne valide pas, ne rend pas et n'appelle rien");
    }

    [Fact]
    public void ApiDelta_core_does_not_depend_on_outer_layers()
    {
        TestResult result = Types.InAssembly(ApiDeltaCoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DevToolbox.Infrastructure",
                "DevToolbox.Presentation",
                "DevToolbox.Tools.ApiDelta.Infrastructure",
                "DevToolbox.Tools.ApiDelta.Presentation",
                "Spectre.Console",
                "System.Net.Http",
                "YamlDotNet",
                "Microsoft.OpenApi")
            .GetResult();

        AssertSatisfied(result);
    }

    [Fact]
    public void ApiDelta_presentation_does_not_depend_on_its_infrastructure()
    {
        TestResult result = Types.InAssembly(ApiDeltaPresentationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DevToolbox.Infrastructure",
                "DevToolbox.Tools.ApiDelta.Infrastructure",
                "YamlDotNet",
                "Microsoft.OpenApi")
            .GetResult();

        AssertSatisfied(result);
    }

    [Fact]
    public void ApiDelta_infrastructure_does_not_depend_on_presentation()
    {
        TestResult result = Types.InAssembly(ApiDeltaInfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DevToolbox.Presentation",
                "DevToolbox.Tools.ApiDelta.Presentation",
                "Spectre.Console")
            .GetResult();

        AssertSatisfied(result);
    }

    [Fact]
    public void ApiDelta_confines_its_only_outbound_call_to_one_namespace()
    {
        // La règle était « aucun appel sortant » tant qu'apidelta ne lisait que des fichiers locaux. Elle
        // s'est resserrée plutôt que lever : récupérer un contrat par son adresse est désormais prévu,
        // mais reste le seul appel sortant de l'outil, et il vit dans un seul espace de noms. Le cœur et
        // la présentation n'y touchent pas, si bien qu'aucune règle de récupération ne peut être
        // contournée en appelant depuis ailleurs.
        foreach (Assembly assembly in new[] { ApiDeltaCoreAssembly, ApiDeltaPresentationAssembly })
        {
            AssertSatisfied(Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("System.Net.Http")
                .GetResult());
        }

        AssertSatisfied(Types.InAssembly(ApiDeltaInfrastructureAssembly)
            .That()
            .DoNotResideInNamespace("DevToolbox.Tools.ApiDelta.Infrastructure.Http")
            .ShouldNot()
            .HaveDependencyOn("System.Net.Http")
            .GetResult());
    }

    [Fact]
    public void ApiDelta_never_writes_over_http()
    {
        // Les méthodes d'écriture HTTP sont déjà bannies partout par RS0030. Cet essai dit la raison :
        // récupérer un contrat est une lecture, et un outil qui saurait écrire vers un service pourrait
        // le modifier — ce qu'aucune de ses promesses n'autorise.
        AssertSatisfied(Types.InAssembly(ApiDeltaInfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("System.Net.Http.Json")
            .GetResult());
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
