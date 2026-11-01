using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class SoapOpenApiYamlExporterTests
{
    [Fact]
    public async Task Exports_real_wsdl_file_to_openapi_yaml_without_exposing_endpoint_host()
    {
        string directory = Path.Combine(Path.GetTempPath(), "soap-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "service.wsdl");
            await File.WriteAllTextAsync(path,
                "<definitions xmlns='http://schemas.xmlsoap.org/wsdl/'"
                + " xmlns:tns='urn:test' xmlns:soap='http://schemas.xmlsoap.org/wsdl/soap/'"
                + " targetNamespace='urn:test'>"
                + "<message name='Input'/><portType name='PortType'>"
                + "<operation name='Ping'><input message='tns:Input'/></operation></portType>"
                + "<binding name='Binding' type='tns:PortType'>"
                + "<soap:binding transport='http://schemas.xmlsoap.org/soap/http'/>"
                + "<operation name='Ping'><soap:operation soapAction='urn:ping'/>"
                + "<input><soap:body use='literal'/></input></operation></binding>"
                + "<service name='Service'><port name='Port' binding='tns:Binding'>"
                + "<soap:address location='https://private.example.test/soap/ping'/>"
                + "</port></service></definitions>");

            string yaml = await SoapOpenApiYamlExporter.ExportFileAsync(
                path, "Test SOAP", "1.0", CancellationToken.None);

            yaml.Should().Contain("openapi: 3.0.3");
            yaml.Should().Contain("/soap/ping");
            yaml.Should().Contain("post:");
            yaml.Should().NotContain("private.example.test");

            using StringReader input = new(yaml);
            YamlStream parsed = new();
            parsed.Load(input);
            parsed.Documents.Should().ContainSingle();
            YamlMappingNode root = (YamlMappingNode)parsed.Documents[0].RootNode;
            root.Children[new YamlScalarNode("openapi")].ToString().Should().Be("3.0.3");
            YamlMappingNode info = (YamlMappingNode)root.Children[new YamlScalarNode("info")];
            info.Children[new YamlScalarNode("title")].ToString().Should().Be("Test SOAP");
            YamlMappingNode paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
            paths.Children.Should().ContainSingle();
            YamlMappingNode endpoint = (YamlMappingNode)paths.Children[new YamlScalarNode("/soap/ping")];
            YamlMappingNode post = (YamlMappingNode)endpoint.Children[new YamlScalarNode("post")];
            YamlMappingNode request = (YamlMappingNode)post.Children[new YamlScalarNode("requestBody")];
            YamlMappingNode content = (YamlMappingNode)request.Children[new YamlScalarNode("content")];
            content.Children.Should().ContainKey(new YamlScalarNode("text/xml"));
            YamlMappingNode responses = (YamlMappingNode)post.Children[new YamlScalarNode("responses")];
            responses.Children.Should().ContainKey(new YamlScalarNode("202"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("https://user:password@private.example.test/soap")]
    [InlineData("https://private.example.test/soap?token=secret")]
    [InlineData("https://private.example.test/soap#fragment")]
    [InlineData("file:///tmp/soap")]
    public async Task Rejects_unsafe_soap_addresses_without_leaking_credentials(string endpoint)
    {
        string directory = Path.Combine(Path.GetTempPath(), "soap-security-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "service.wsdl");
            string xml = "<definitions xmlns='http://schemas.xmlsoap.org/wsdl/'"
                + " xmlns:tns='urn:test' xmlns:soap='http://schemas.xmlsoap.org/wsdl/soap/'"
                + " targetNamespace='urn:test'>"
                + "<message name='Input'/><portType name='PortType'>"
                + "<operation name='Ping'><input message='tns:Input'/></operation></portType>"
                + "<binding name='Binding' type='tns:PortType'>"
                + "<soap:binding transport='http://schemas.xmlsoap.org/soap/http'/>"
                + "<operation name='Ping'><soap:operation soapAction='urn:ping'/>"
                + "<input><soap:body use='literal'/></input></operation></binding>"
                + "<service name='Service'><port name='Port' binding='tns:Binding'>"
                + "<soap:address location='" + endpoint + "'/>"
                + "</port></service></definitions>";
            await File.WriteAllTextAsync(path, xml);

            Func<Task<string>> export = () => SoapOpenApiYamlExporter.ExportFileAsync(
                path, "Test SOAP", "1.0", CancellationToken.None);

            await export.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Refuses_structurally_invalid_generated_openapi_before_publication()
    {
        Dictionary<string, object?> malformed = new(StringComparer.Ordinal)
        {
            ["openapi"] = "3.0.3",
            ["info"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "SOAP",
            },
            ["paths"] = new Dictionary<string, object?>(StringComparer.Ordinal),
        };

        Action validate = () => SoapOpenApiYamlExporter.ValidateOpenApi(malformed);

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*OpenAPI*invalide*");
    }

    [Fact]
    public void Refuses_contracts_without_soap_endpoints_instead_of_exporting_empty_paths()
    {
        WsdlContract contract = new([], [],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        Action export = () => SoapOpenApiYamlExporter.Export(contract, "SOAP", "1.0");

        export.Should().Throw<InvalidOperationException>().WithMessage("*endpoint SOAP*");
    }

    [Fact]
    public void Refuses_unresolved_graphs_before_export()
    {
        WsdlContract contract = new([], [],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [],
            [new UnresolvedImport("urn:missing", "missing.xsd",
                new WsdlLocation("root.wsdl", "import", 1, 1), "not found")], ".");

        Action export = () => SoapOpenApiYamlExporter.Export(contract, "SOAP", "1.0");

        export.Should().Throw<InvalidOperationException>().WithMessage("*entièrement résolu*");
    }
}
