using DevToolbox.Infrastructure.AzureDevOps;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Le nom court par lequel un serveur se désigne en interne traverse toute la configuration.
/// </summary>
/// <remarks>
/// Le chemin complet compte autant que la lecture elle-même : une adresse acceptée à l'invite puis rejetée
/// à la validation, ou acceptée par les deux et refusée par le client HTTP, serait une panne au démarrage
/// pour la saisie la plus courante de la maison.
/// </remarks>
public sealed class ServerAddressConfigurationTests
{
    [Theory]
    [InlineData("azure")]
    [InlineData("azure/")]
    [InlineData("https://azure")]
    public void A_short_host_name_binds_validates_and_becomes_an_absolute_address(string configured)
    {
        using ServiceProvider provider = BuildProvider(configured);

        AzureDevOpsServerOptions options =
            provider.GetRequiredService<IOptions<AzureDevOpsServerOptions>>().Value;

        options.BaseUrl.Should().Be("https://azure/");

        // Ce que fait le client typé au moment de fixer son adresse de base.
        new Uri(options.BaseUrl, UriKind.Absolute).Host.Should().Be("azure");
    }

    [Fact]
    public void An_address_that_cannot_be_read_still_fails_with_the_setting_named()
    {
        using ServiceProvider provider = BuildProvider("ftp://azure");

        Action resolve = () => _ = provider.GetRequiredService<IOptions<AzureDevOpsServerOptions>>().Value;

        resolve.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Contain(failure => failure.Contains("AzureDevOpsServer:BaseUrl"));
    }

    private static ServiceProvider BuildProvider(string baseUrl)
    {
        ConfigurationManager configuration = new();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AzureDevOpsServer:BaseUrl"] = baseUrl,
            ["AzureDevOpsServer:Collection"] = "DefaultCollection",
            ["AzureDevOpsServer:ApiVersion"] = "7.1",
        });

        ServiceCollection services = [];
        services.AddLogging(builder => builder.ClearProviders());
        services.AddAzureDevOps(configuration, "appsettings.json");

        return services.BuildServiceProvider();
    }
}
