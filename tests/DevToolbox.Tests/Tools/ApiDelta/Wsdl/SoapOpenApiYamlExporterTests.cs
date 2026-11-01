using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class SoapOpenApiYamlExporterTests
{
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
