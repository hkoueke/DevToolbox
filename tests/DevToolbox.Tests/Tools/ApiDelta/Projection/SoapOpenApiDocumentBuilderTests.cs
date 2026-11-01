using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

public sealed class SoapOpenApiDocumentBuilderTests
{
    [Theory]
    [InlineData(SoapVersion.Soap11, "text/xml")]
    [InlineData(SoapVersion.Soap12, "application/soap+xml")]
    public void Build_exposes_bound_soap_operation_as_post_with_xml_body(
        SoapVersion soap, string mediaType)
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlMessage input = new("Request", [], location);
        WsdlMessage output = new("Response", [], location);
        WsdlBinding binding = new("Port", "document", "literal", null, soap, "urn:read", location);
        WsdlOperation operation = new(
            "Read", MessagePattern.RequestResponse, input, output, [], binding, null, location);
        WsdlContract contract = new(
            [], [operation], new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(contract, "Service SOAP", "1.0", "/soap");
        SchemaDraft post = document.Section("paths").Section("/soap").Section("post");

        document.Get("openapi").Should().Be("3.0.3");
        post.Get("operationId").Should().Be("Read");
        post.Get("x-soap-action").Should().Be("urn:read");
        SchemaDraft request = post.Section("requestBody");
        request.Get("required").Should().Be(true);
        request.Section("content").Section(mediaType).Section("schema")
            .Get("type").Should().Be("string");
        post.Section("responses").Section("200").Section("content")
            .Section(mediaType).Section("schema").Get("type").Should().Be("string");
    }


    [Fact]
    public void Build_preserves_explicitly_empty_soap_action()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding binding = new("Port", "document", "literal", null, SoapVersion.Soap11, "", location);
        WsdlOperation operation = new(
            "Read", MessagePattern.OneWay, null, null, [], binding, null, location);
        WsdlContract contract = new(
            [], [operation], new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(contract, "Service", "1.0", "/soap");

        document.Section("paths").Section("/soap").Section("post")
            .Get("x-soap-action").Should().Be("");
    }

    [Theory]
    [InlineData(MessagePattern.Notification)]
    [InlineData(MessagePattern.SolicitResponse)]
    public void Build_skips_service_initiated_message_patterns(MessagePattern pattern)
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding binding = new("Port", "document", "literal", null, SoapVersion.Soap11, null, location);
        WsdlOperation operation = new(
            "Callback", pattern, null, null, [], binding, null, location);
        WsdlContract contract = new(
            [], [operation], new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(contract, "Service", "1.0", "/soap");

        document.Section("paths").Keywords.Should().BeEmpty();
    }

    [Fact]
    public void Build_rejects_multiple_operations_on_one_http_post_endpoint()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding binding = new("Port", "document", "literal", null, SoapVersion.Soap11, null, location);
        WsdlOperation first = new(
            "Read", MessagePattern.OneWay, null, null, [], binding, null, location);
        WsdlOperation second = first with { Name = "Write" };
        WsdlContract contract = new(
            [], [first, second], new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        Action build = () => SoapOpenApiDocumentBuilder.Build(contract, "Service", "1.0", "/soap");

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*même route OpenAPI*");
    }

    [Fact]
    public void Build_does_not_invent_http_routes_for_unbound_operations()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlOperation operation = new(
            "Unbound", MessagePattern.OneWay, null, null, [], null, null, location);
        WsdlContract contract = new(
            [], [operation], new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(contract, "Service", "1.0", "/soap");

        document.Section("paths").Keywords.Should().BeEmpty();
    }
}
