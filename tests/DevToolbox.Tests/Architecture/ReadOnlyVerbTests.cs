using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Architecture;

/// <summary>
/// Protège la garantie de lecture seule.
/// </summary>
/// <remarks>
/// <para>
/// Ce n'est pas ce test qui fait respecter la règle, mais <c>BannedSymbols.txt</c>, qui transforme tout
/// verbe autre que GET en erreur de compilation sur le site d'appel. C'est plus fort qu'un test : un test ne
/// peut qu'inspecter ce qui a été écrit, tandis que l'analyseur empêche de l'écrire.
/// </para>
/// <para>
/// Ce que ce test protège, c'est la <em>configuration</em> de ce garde-fou. Supprimer une ligne de la liste
/// rouvrirait silencieusement le chemin d'écriture : les entrées obligatoires sont donc vérifiées ici.
/// </para>
/// </remarks>
public sealed class ReadOnlyVerbTests
{
    private static readonly string[] RequiredBans =
    [
        "P:System.Net.Http.HttpMethod.Post",
        "P:System.Net.Http.HttpMethod.Put",
        "P:System.Net.Http.HttpMethod.Patch",
        "P:System.Net.Http.HttpMethod.Delete",
        "M:System.Net.Http.HttpClient.#ctor",
        "T:System.Console",
    ];

    [Fact]
    public void Every_state_changing_verb_is_banned_at_the_call_site()
    {
        string banList = File.ReadAllText(BannedSymbolsPath());

        foreach (string required in RequiredBans)
        {
            banList.Should().Contain(
                required,
                "supprimer cette interdiction rouvrirait un chemin d'écriture");
        }
    }

    [Fact]
    public void The_gateway_source_contains_no_state_changing_verb()
    {
        // Ceinture et bretelles : l'analyseur couvre déjà ce point, mais lire la source rend la garantie
        // lisible pour un relecteur qui ignore l'existence de la liste d'API bannies.
        string gateway = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "tools", "VarCompare",
                "DevToolbox.Tools.VarCompare.Infrastructure", "VariableGroupGateway.cs"));

        foreach (string verb in new[] { "HttpMethod.Post", "HttpMethod.Put", "HttpMethod.Patch", "HttpMethod.Delete" })
        {
            gateway.Should().NotContain(verb);
        }
    }

    private static string BannedSymbolsPath() =>
        Path.Combine(RepositoryRoot(), "BannedSymbols.txt");

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
