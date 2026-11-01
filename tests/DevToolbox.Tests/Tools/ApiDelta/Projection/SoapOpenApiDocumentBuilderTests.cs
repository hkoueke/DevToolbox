using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

public sealed class SoapOpenApiDocumentBuilderTests
{
    [Fact]
    public void Build_assigns_unique_operation_ids_across_distinct_soap_endpoints()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding firstBinding = new("FirstBinding", "document", "literal",
            null, SoapVersion.Soap11, null, location);
        WsdlBinding secondBinding = firstBinding with { Name = "SecondBinding" };
        WsdlOperation first = new("Ping", MessagePattern.OneWay,
            null, null, [], firstBinding, null, location);
        WsdlOperation second = first with { Binding = secondBinding };
        WsdlContract contract = new([], [first, second],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");
        Dictionary<string, string> endpoints = new(StringComparer.Ordinal)
        {
            ["FirstBinding"] = "/a",
            ["SecondBinding"] = "/b",
        };

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(
            contract, "SOAP", "1.0", endpoints);

        document.Section("paths").Section("/a").Section("post")
            .Get("operationId").Should().Be("Ping");
        document.Section("paths").Section("/b").Section("post")
            .Get("operationId").Should().Be("Ping_2");
    }

    [Fact]
    public void Build_assigns_unique_ids_to_shared_and_single_operation_endpoints()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding firstBinding = new("FirstBinding", "document", "literal",
            null, SoapVersion.Soap11, null, location);
        WsdlBinding secondBinding = firstBinding with { Name = "SecondBinding" };
        WsdlBinding thirdBinding = firstBinding with { Name = "ThirdBinding" };
        WsdlOperation first = new("First", MessagePattern.OneWay,
            null, null, [], firstBinding, null, location);
        WsdlOperation second = first with { Name = "Second", Binding = secondBinding };
        WsdlOperation third = first with { Name = "soapEndpoint", Binding = thirdBinding };
        WsdlContract contract = new([], [first, second, third],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");
        Dictionary<string, string> endpoints = new(StringComparer.Ordinal)
        {
            ["FirstBinding"] = "/a",
            ["SecondBinding"] = "/a",
            ["ThirdBinding"] = "/b",
        };

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(
            contract, "SOAP", "1.0", endpoints);

        SchemaDraft shared = document.Section("paths").Section("/a").Section("post");
        shared.Get("operationId").Should().Be("soapEndpoint");
        shared.Get("description").Should().BeOfType<string>()
            .Which.Should().Contain("First").And.Contain("Second");
        shared.Get("description").Should().BeOfType<string>()
            .Which.Should().Contain("\n- Second");
        document.Section("paths").Section("/b").Section("post")
            .Get("operationId").Should().Be("soapEndpoint_2");
    }

    [Fact]
    public void Build_exposes_declared_soap_faults_as_default_response()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding binding = new("Binding", "document", "literal",
            null, SoapVersion.Soap11, null, location);
        WsdlMessage fault = new("urn:test:ValidationFault", [], location);
        WsdlOperation operation = new("Call", MessagePattern.OneWay,
            null, null, [fault], binding, null, location);
        WsdlContract contract = new([], [operation],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(
            contract, "SOAP", "1.0", "/soap");

        SchemaDraft responses = document.Section("paths").Section("/soap")
            .Section("post").Section("responses");
        responses.Section("default").Get("description").Should().BeOfType<string>()
            .Which.Should().Contain("ValidationFault");
        responses.Section("default").Section("content").Keywords.Keys.Should().Contain("text/xml");
    }

    [Theory]
    [InlineData("//secret.example/soap")]
    [InlineData("/soap?token=secret")]
    [InlineData("/soap#fragment")]
    [InlineData("/soap\\\\admin")]
    [InlineData("https://example.test/soap")]
    public void Build_rejects_unsafe_endpoint_paths_in_both_overloads(string path)
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding binding = new("Binding", "document", "literal",
            null, SoapVersion.Soap11, null, location);
        WsdlOperation operation = new("Call", MessagePattern.OneWay,
            null, null, [], binding, null, location);
        WsdlContract contract = new([], [operation],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");
        Dictionary<string, string> paths = new(StringComparer.Ordinal) { ["Binding"] = path };

        Action single = () => SoapOpenApiDocumentBuilder.Build(contract, "SOAP", "1.0", path);
        Action multiple = () => SoapOpenApiDocumentBuilder.Build(contract, "SOAP", "1.0", paths);

        single.Should().Throw<ArgumentException>();
        multiple.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_routes_multiple_bindings_to_explicit_endpoints_without_inventing_paths()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding firstBinding = new("OrdersBinding", "document", "literal",
            null, SoapVersion.Soap11, "urn:orders", location);
        WsdlBinding secondBinding = new("AccountsBinding", "document", "literal",
            null, SoapVersion.Soap12, "urn:accounts", location);
        WsdlOperation first = new("Orders", MessagePattern.OneWay,
            null, null, [], firstBinding, null, location);
        WsdlOperation second = new("Accounts", MessagePattern.OneWay,
            null, null, [], secondBinding, null, location);
        WsdlContract contract = new([], [first, second],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");
        Dictionary<string, string> endpoints = new(StringComparer.Ordinal)
        {
            ["OrdersBinding"] = "/services/orders",
            ["AccountsBinding"] = "/services/accounts",
        };

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(
            contract, "SOAP", "1.0", endpoints);

        SchemaDraft paths = document.Section("paths");
        paths.Keywords.Keys.Should().BeEquivalentTo("/services/orders", "/services/accounts");
        paths.Section("/services/orders").Section("post").Get("operationId").Should().Be("Orders");
        paths.Section("/services/accounts").Section("post").Get("operationId").Should().Be("Accounts");
    }

    [Fact]
    public void Build_groups_distinct_bindings_sharing_the_same_explicit_endpoint()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding firstBinding = new("FirstBinding", "document", "literal",
            null, SoapVersion.Soap11, "urn:first", location);
        WsdlBinding secondBinding = firstBinding with { Name = "SecondBinding", SoapAction = "urn:second" };
        WsdlOperation first = new("First", MessagePattern.OneWay,
            null, null, [], firstBinding, null, location);
        WsdlOperation second = first with { Name = "Second", Binding = secondBinding };
        WsdlContract contract = new([], [first, second],
            new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");
        Dictionary<string, string> endpoints = new(StringComparer.Ordinal)
        {
            ["FirstBinding"] = "/soap",
            ["SecondBinding"] = "/soap",
        };

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(contract, "SOAP", "1.0", endpoints);

        document.Section("paths").Keywords.Keys.Should().ContainSingle().Which.Should().Be("/soap");
        SchemaDraft post = document.Section("paths").Section("/soap").Section("post");
        post.Get("description").Should().BeOfType<string>()
            .Which.Should().Contain("First").And.Contain("Second");
    }

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
        post.Get("description").Should().BeOfType<string>()
            .Which.Should().Contain("urn:read");
        post.Keywords.Keys.Should().NotContain(key => key.StartsWith("x-", StringComparison.Ordinal));
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
            .Get("description").Should().BeOfType<string>()
            .Which.Should().Contain("SOAPAction = " + (char)34 + (char)34);
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
    public void Build_represents_multiple_operations_on_one_http_post_endpoint()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding binding = new("Port", "document", "literal", null, SoapVersion.Soap11, null, location);
        WsdlOperation first = new(
            "Read", MessagePattern.OneWay, null, null, [], binding, null, location);
        WsdlOperation second = first with { Name = "Write" };
        WsdlContract contract = new(
            [], [first, second], new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(contract, "Service", "1.0", "/soap");
        SchemaDraft paths = document.Section("paths");
        paths.Keywords.Should().ContainSingle();
        SchemaDraft post = paths.Section("/soap").Section("post");
        post.Get("operationId").Should().Be("soapEndpoint");
        post.Get("description").Should().BeOfType<string>()
            .Which.Should().Contain("Read").And.Contain("Write");
        post.Keywords.Keys.Should().NotContain(key => key.StartsWith("x-", StringComparison.Ordinal));
        post.Section("requestBody").Section("content").Section("text/xml")
            .Section("schema").Get("type").Should().Be("string");
    }

    [Fact]
    public void Build_shared_one_way_endpoint_does_not_invent_response_body()
    {
        WsdlLocation location = new("service.wsdl", "operation", 1, 1);
        WsdlBinding binding = new("Port", "document", "literal", null, SoapVersion.Soap11, null, location);
        WsdlOperation first = new(
            "NotifyA", MessagePattern.OneWay, null, null, [], binding, null, location);
        WsdlOperation second = first with { Name = "NotifyB" };
        WsdlContract contract = new(
            [], [first, second], new Dictionary<string, XsdType>(StringComparer.Ordinal), [], [], ".");

        SchemaDraft document = SoapOpenApiDocumentBuilder.Build(contract, "Service", "1.0", "/soap");
        SchemaDraft responses = document.Section("paths").Section("/soap")
            .Section("post").Section("responses");

        responses.Keywords.Keys.Should().ContainSingle().Which.Should().Be("202");
        responses.Section("202").Keywords.Keys.Should().NotContain("content");
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
