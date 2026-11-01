using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class OpenApiYamlExporterTests
{
    [Fact]
    public void Export_preserves_openapi_version_and_quoted_schema_reference()
    {
        SchemaDraft draft = new();
        draft.Set("openapi", "3.0.3");
        draft.Section("info").Set("title", "SOAP API");
        draft.Section("info").Set("version", "1.0");
        draft.Section("components").Section("schemas").Section("Envelope")
            .Set("$ref", "#/components/schemas/SoapBody");

        string yaml = Encoding.UTF8.GetString(new OpenApiYamlExporter().Export(draft).Span);

        yaml.Should().Contain("openapi: 3.0.3");
        yaml.Should().Contain("$ref: '#/components/schemas/SoapBody'");
        yaml.Should().Contain("title: SOAP API");
    }
}
