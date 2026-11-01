using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class SoapEndpointPathReaderTests
{
    [Fact]
    public async Task Extracts_only_paths_from_multiple_services_without_leaking_authorities()
    {
        string directory = Path.Combine(Path.GetTempPath(), "soap-endpoints-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "services.wsdl");
            await File.WriteAllTextAsync(path, Wsdl(
                "<service name='Orders'><port name='OrdersPort' binding='tns:OrdersBinding'><soap:address location='https://internal.example.test/services/orders'/></port></service>"
                + "<service name='Accounts'><port name='AccountsPort' binding='tns:AccountsBinding'><soap:address location='https://private.example.test/services/accounts'/></port></service>"));
            WsdlReadResult result = await new WsdlReader().ReadAsync(
                path, WsdlLimits.Default, ImportPolicy.ConfinedTo(directory), CancellationToken.None);
            result.IsFaithful.Should().BeTrue();
            IReadOnlyDictionary<string, string> endpoints = SoapEndpointPathReader.Read(result.Contract!);
            endpoints.Should().Contain("OrdersBinding", "/services/orders");
            endpoints.Should().Contain("AccountsBinding", "/services/accounts");
            string.Join(" ", endpoints.Values).Should().NotContain("example.test");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("https://user:password@example.test/soap")]
    [InlineData("https://example.test/soap?token=secret")]
    [InlineData("ftp://example.test/soap")]
    public async Task Refuses_addresses_with_credentials_queries_or_non_http_schemes(string address)
    {
        string directory = Path.Combine(Path.GetTempPath(), "soap-endpoints-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "services.wsdl");
            await File.WriteAllTextAsync(path, Wsdl(
                "<service name='Orders'><port name='Port' binding='tns:OrdersBinding'><soap:address location='"
                + address.Replace("&", "&amp;", StringComparison.Ordinal)
                + "'/></port></service>"));
            WsdlReadResult result = await new WsdlReader().ReadAsync(
                path, WsdlLimits.Default, ImportPolicy.ConfinedTo(directory), CancellationToken.None);
            result.IsFaithful.Should().BeTrue();
            Action extract = () => SoapEndpointPathReader.Read(result.Contract!);
            extract.Should().Throw<InvalidOperationException>().WithMessage("*OrdersBinding*");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string Wsdl(string services) =>
        "<definitions xmlns='http://schemas.xmlsoap.org/wsdl/'"
        + " xmlns:tns='urn:test' xmlns:soap='http://schemas.xmlsoap.org/wsdl/soap/'"
        + " targetNamespace='urn:test'>"
        + "<portType name='PortType'/><binding name='OrdersBinding' type='tns:PortType'>"
        + "<soap:binding transport='http://schemas.xmlsoap.org/soap/http'/></binding>"
        + "<binding name='AccountsBinding' type='tns:PortType'>"
        + "<soap:binding transport='http://schemas.xmlsoap.org/soap/http'/></binding>"
        + services + "</definitions>";
}
