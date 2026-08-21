using DevToolbox.Domain.AzureDevOps;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Domain;

/// <summary>
/// Ce qu'un développeur écrit réellement quand on lui demande où est son serveur.
/// </summary>
/// <remarks>
/// La forme courante en interne est le nom court : <c>azure</c>. Exiger la forme canonique reviendrait à
/// demander une saisie qu'il n'écrit nulle part ailleurs, et c'est précisément ce que ces cas verrouillent.
/// </remarks>
public sealed class ServerAddressTests
{
    [Theory]
    [InlineData("azure", "https://azure/")]
    [InlineData("azure/", "https://azure/")]
    [InlineData("  azure  ", "https://azure/")]
    [InlineData("AZURE", "https://azure/")]
    [InlineData("azure.entreprise.local", "https://azure.entreprise.local/")]
    public void A_bare_host_name_is_read_as_an_https_address(string typed, string expected)
    {
        ServerAddress.TryNormalise(typed, allowInsecureHttp: false, out Uri? address, out _)
            .Should().BeTrue();

        address!.AbsoluteUri.Should().Be(expected);
    }

    [Fact]
    public void A_port_is_not_mistaken_for_a_scheme()
    {
        // « azure:8080 » se lit comme un hôte et un port. Une lecture naïve y verrait le schéma « azure ».
        ServerAddress.TryNormalise("azure:8080", allowInsecureHttp: false, out Uri? address, out _)
            .Should().BeTrue();

        address!.Host.Should().Be("azure");
        address.Port.Should().Be(8080);
        address.Scheme.Should().Be(Uri.UriSchemeHttps);
    }

    [Fact]
    public void A_virtual_directory_survives_normalisation()
    {
        // Les installations derrière /tfs en dépendent : le chemin fait partie de l'adresse de base.
        ServerAddress.TryNormalise("azure/tfs", allowInsecureHttp: false, out Uri? address, out _)
            .Should().BeTrue();

        address!.AbsoluteUri.Should().Be("https://azure/tfs");
    }

    [Fact]
    public void An_explicit_scheme_is_honoured_and_never_replaced()
    {
        ServerAddress.TryNormalise(
                "https://devops.entreprise.local", allowInsecureHttp: false, out Uri? address, out _)
            .Should().BeTrue();

        address!.AbsoluteUri.Should().Be("https://devops.entreprise.local/");
    }

    [Fact]
    public void A_query_and_a_fragment_are_dropped_rather_than_carried_onto_every_route()
    {
        ServerAddress.TryNormalise(
                "https://azure/tfs?x=1#y", allowInsecureHttp: false, out Uri? address, out _)
            .Should().BeTrue();

        address!.AbsoluteUri.Should().Be("https://azure/tfs");
    }

    [Fact]
    public void Plain_http_is_refused_unless_it_was_deliberately_allowed()
    {
        ServerAddress.TryNormalise("http://azure", allowInsecureHttp: false, out _, out ServerAddressProblem refused)
            .Should().BeFalse();

        refused.Should().Be(ServerAddressProblem.InsecureScheme);

        ServerAddress.TryNormalise("http://azure", allowInsecureHttp: true, out Uri? allowed, out _)
            .Should().BeTrue();

        allowed!.AbsoluteUri.Should().Be("http://azure/");
    }

    [Theory]
    [InlineData("", ServerAddressProblem.Empty)]
    [InlineData("   ", ServerAddressProblem.Empty)]
    [InlineData("ftp://azure", ServerAddressProblem.UnsupportedScheme)]
    [InlineData("https://someone:secret@azure", ServerAddressProblem.CarriesCredentials)]
    public void What_cannot_be_used_is_named_rather_than_silently_accepted(
        string typed,
        ServerAddressProblem expected)
    {
        ServerAddress.TryNormalise(typed, allowInsecureHttp: false, out Uri? address, out ServerAddressProblem problem)
            .Should().BeFalse();

        address.Should().BeNull();
        problem.Should().Be(expected);
    }
}
