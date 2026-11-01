using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Presentation;
using DevToolbox.Domain.Runs;
using Spectre.Console.Testing;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class SoapExportIntegrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "soap-integrated-" + Guid.NewGuid().ToString("N"));

    public SoapExportIntegrationTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("http://schemas.xmlsoap.org/wsdl/soap/", "text/xml", "http://schemas.xmlsoap.org/soap/envelope/")]
    [InlineData("http://schemas.xmlsoap.org/wsdl/soap12/", "application/soap+xml", "http://www.w3.org/2003/05/soap-envelope")]
    public async Task Resolves_imported_QNames_and_projects_XML_types_messages_and_faults(
        string soap, string media, string envelope)
    {
        string path = Contract(soap);
        string yaml = await Export(path);
        YamlMappingNode document = Parse(yaml);
        YamlMappingNode post = Map(document, "paths", "/actual", "post");
        YamlMappingNode request = Map(post, "requestBody", "content", media, "schema");
        request.ToString().Should().Contain("Envelope").And.Contain(envelope).And.Contain("Request");
        Map(post, "responses", "200", "content", media).ToString().Should().Contain("Reply");
        Map(post, "responses", "default", "content", media).ToString().Should().Contain("Fault").And.Contain("Failure");
        yaml.Should().Contain("wrapped: false").And.Contain("minItems: 0").And.Contain("maxItems: 3")
            .And.Contain("int32").And.Contain("urn:types").And.Contain("SOAPAction = \"\"");
        yaml.Should().NotContain("private.example").And.NotContain("x-soap-");
        Map(document, "components", "schemas").Children.Should().HaveCountGreaterThan(1);
        (await Export(path)).Should().Be(yaml, "serialization is deterministic for the same graph");
    }

    [Fact]
    public async Task Shared_endpoint_keeps_one_way_acknowledgement_and_response_media_separate()
    {
        string path = Contract();
        string bindingPath = Path.Combine(_directory, "binding.wsdl");
        string modelPath = Path.Combine(_directory, "model.wsdl");
        await File.WriteAllTextAsync(bindingPath, (await File.ReadAllTextAsync(bindingPath)).Replace(
            "</binding>", "<operation name='Send'><soap:operation/><input><soap:body use='literal'/></input></operation></binding>", StringComparison.Ordinal));
        await File.WriteAllTextAsync(modelPath, (await File.ReadAllTextAsync(modelPath)).Replace(
            "</portType>", "<operation name='Send'><input message='tns:RequestMessage'/></operation></portType>", StringComparison.Ordinal));
        YamlMappingNode document = Parse(await Export(path));
        YamlMappingNode responses = Map(document, "paths", "/actual", "post", "responses");
        responses.Children.Should().ContainKey(new YamlScalarNode("200")).And.ContainKey(new YamlScalarNode("202"));
        Map(responses, "202").Children.Should().NotContainKey(new YamlScalarNode("content"));
    }

    [Theory]
    [InlineData("binding='b:Shared'", "binding='missing:Shared' xmlns:missing='urn:wrong'", "QNAME_UNRESOLVED")]
    [InlineData("binding='b:Shared'", "binding='missing:Shared'", "QNAME_PREFIX_UNDECLARED")]
    [InlineData("binding='b:Shared'", "binding='b:Shared:Other'", "QNAME_INVALID")]
    public async Task Port_references_never_fall_back_to_a_local_binding_name(string old, string replacement, string reason)
    {
        string path = Contract();
        await Replace(path, old, replacement);
        await Refused(path, reason);
    }

    [Theory]
    [InlineData("<soap:binding style='document'", "<soap:binding style='rpc'", "SOAP_RPC_UNSUPPORTED")]
    [InlineData("<soap:body use='literal'/>", "<soap:body use='encoded'/>", "SOAP_ENCODED_OR_MISSING_LITERAL_BODY_UNSUPPORTED")]
    [InlineData("<soap:body use='literal'/>", "<soap:body use='literal' parts='missing'/>", "SOAP_BODY_PART_UNRESOLVED")]
    [InlineData("<soap:body use='literal'/>", "<soap:body use='literal'/><soap:header/>", "SOAP_HEADERS_OR_NON_BODY_EXTENSIONS_UNSUPPORTED")]
    public async Task Unsupported_binding_semantics_are_reported_instead_of_projected(string old, string replacement, string reason)
    {
        string path = Contract();
        await Replace(Path.Combine(_directory, "binding.wsdl"), old, replacement);
        await Refused(path, reason);
    }

    [Theory]
    [InlineData("<xs:complexType name='Payload'>", "<xs:complexType name='Payload' mixed='true'>", "XSD_MIXED_CONTENT_UNSUPPORTED")]
    [InlineData("</xs:complexType>", "<xs:attribute name='flag' type='xs:string'/></xs:complexType>", "XSD_ATTRIBUTES_UNSUPPORTED")]
    [InlineData("name='Request' type='tns:Payload'", "name='Request' type='tns:Payload' abstract='true'", "XSD_SUBSTITUTION_GROUP_OR_ABSTRACT_ELEMENT_UNSUPPORTED")]
    [InlineData("<xs:sequence>", "<xs:sequence minOccurs='0'>", "XSD_CHOICE_WILDCARD_OR_GROUP_CARDINALITY_UNSUPPORTED")]
    [InlineData("name='count' type='xs:int'", "name='count' type='xs:date'", "XSD_PRIMITIVE_UNSUPPORTED")]
    public async Task Unsupported_XSD_is_explicit_and_no_plausible_schema_is_returned(string old, string replacement, string reason)
    {
        string path = Contract();
        await Replace(Path.Combine(_directory, "types.xsd"), old, replacement);
        await Refused(path, reason);
    }

    [Fact]
    public async Task Substitution_group_members_are_not_silently_discarded()
    {
        string path = Contract();
        await Replace(Path.Combine(_directory, "types.xsd"), "</xs:schema>",
            "<xs:element name='Special' type='tns:Payload' substitutionGroup='tns:Request'/></xs:schema>");
        await Refused(path, "XSD_SUBSTITUTION_GROUP_OR_ABSTRACT_ELEMENT_UNSUPPORTED");
    }

    [Theory]
    [InlineData("https://user:password@private.example/actual")]
    [InlineData("https://private.example/actual?token=secret")]
    [InlineData("https://private.example/actual#secret")]
    [InlineData("https://private.example/%2f%2fprivate")]
    [InlineData("https://private.example/%0asecret")]
    [InlineData("file:///private/actual")]
    public async Task Hostile_endpoints_are_rejected_without_disclosing_the_URL(string endpoint)
    {
        string path = Contract();
        await Replace(path, "https://private.example/actual", endpoint);
        Func<Task> export = async () => _ = await Export(path);
        var refusal = await export.Should().ThrowAsync<WsdlExportException>();
        refusal.Which.Message.Should().NotContain("private.example").And.NotContain("password").And.NotContain("secret");
    }

    [Theory]
    [InlineData("https://private.example/binding.wsdl")]
    [InlineData("../binding.wsdl")]
    public async Task Remote_and_parent_traversal_imports_are_refused(string import)
    {
        string path = Contract();
        await Replace(path, "location='binding.wsdl'", "location='" + import + "'");
        await Refused(path, "WSDL_XSD_GRAPH_UNRESOLVED_OR_INVALID");
    }

    [Fact]
    public async Task Symlink_imports_cannot_escape_the_selected_directory()
    {
        string outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".wsdl");
        try
        {
            string path = Contract();
            File.Move(Path.Combine(_directory, "binding.wsdl"), outside);
            File.CreateSymbolicLink(Path.Combine(_directory, "binding.wsdl"), outside);
            await Refused(path, "WSDL_XSD_GRAPH_UNRESOLVED_OR_INVALID");
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Duplicate_qualified_binding_is_ambiguous_even_when_its_operations_match()
    {
        string path = Contract();
        string binding = await File.ReadAllTextAsync(Path.Combine(_directory, "binding.wsdl"));
        string declaration = binding[binding.IndexOf("<binding ", StringComparison.Ordinal)..binding.IndexOf("</binding>", StringComparison.Ordinal)] + "</binding>";
        await Replace(Path.Combine(_directory, "binding.wsdl"), "</definitions>", declaration + "</definitions>");
        await Refused(path, "WSDL_DECLARATION_MISSING_OR_AMBIGUOUS");
    }

    [Fact]
    public async Task Same_local_binding_name_in_two_namespaces_selects_the_qualified_target()
    {
        string path = Contract();
        string copy = await File.ReadAllTextAsync(Path.Combine(_directory, "binding.wsdl"));
        Write("other.wsdl", copy.Replace("urn:binding", "urn:other", StringComparison.Ordinal)
            .Replace("soapAction=''", "soapAction='urn:wrong'", StringComparison.Ordinal));
        await Replace(path, "<service", "<import namespace='urn:other' location='other.wsdl'/><service");
        string yaml = await Export(path);
        yaml.Should().Contain("SOAPAction = \"\"").And.NotContain("urn:wrong");
    }

    [Fact]
    public async Task Missing_qualified_message_cannot_resolve_to_a_homonym()
    {
        string path = Contract();
        await Replace(Path.Combine(_directory, "model.wsdl"), "message='tns:RequestMessage'",
            "message='wrong:RequestMessage' xmlns:wrong='urn:wrong'");
        await Refused(path, "QNAME_UNRESOLVED");
    }

    [Fact]
    public async Task XSD_symlinks_cannot_escape_the_selected_directory()
    {
        string outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".xsd");
        try
        {
            string path = Contract();
            File.Move(Path.Combine(_directory, "types.xsd"), outside);
            File.CreateSymbolicLink(Path.Combine(_directory, "types.xsd"), outside);
            await Refused(path, "WSDL_XSD_GRAPH_UNRESOLVED_OR_INVALID");
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Projection_depth_budget_is_enforced_for_named_type_chains()
    {
        string path = Contract();
        List<string> types = [];
        for (int index = 0; index < 70; index++)
        {
            string next = index == 69 ? "xs:string" : "tns:T" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            types.Add("<xs:complexType name='T" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "'><xs:sequence><xs:element name='child' type='" + next + "'/></xs:sequence></xs:complexType>");
        }

        await Replace(Path.Combine(_directory, "types.xsd"), "</xs:schema>", string.Concat(types) + "</xs:schema>");
        await Replace(Path.Combine(_directory, "types.xsd"), "name='Request' type='tns:Payload'", "name='Request' type='tns:T0'");
        await Refused(path, "XSD_PROJECTION_DEPTH_LIMIT");
    }

    [Fact]
    public async Task Console_runs_real_export_and_publishes_only_the_validated_YAML()
    {
        string path = Contract();
        string target = Path.Combine(_directory, "result.yaml");
        TestConsole console = ConsoleFor(path, target);
        ToolRun run = await new WsdlOpenApiTool(new WsdlOpenApiExporter(), console).RunAsync(CancellationToken.None);
        run.Outcome.Should().Be(RunOutcome.Success);
        Parse(await File.ReadAllTextAsync(target)).Children.Should().ContainKey(new YamlScalarNode("paths"));
        Directory.GetFiles(_directory, "*.tmp").Should().BeEmpty();
        (await File.ReadAllTextAsync(path)).Should().Contain("private.example");
    }

    [Fact]
    public async Task Console_reports_unsupported_XML_without_creating_an_output_file()
    {
        string path = Contract();
        await Replace(Path.Combine(_directory, "types.xsd"), "</xs:complexType>",
            "<xs:attribute name='flag' type='xs:string'/></xs:complexType>");
        string target = Path.Combine(_directory, "result.yaml");
        TestConsole console = ConsoleFor(path, target);
        ToolRun run = await new WsdlOpenApiTool(new WsdlOpenApiExporter(), console).RunAsync(CancellationToken.None);
        run.Outcome.Should().Be(RunOutcome.Failed);
        console.Output.Should().Contain("XSD_ATTRIBUTES_UNSUPPORTED").And.NotContain("private.example");
        File.Exists(target).Should().BeFalse();
    }

    [Fact]
    public async Task Console_never_overwrites_an_existing_file()
    {
        string path = Contract();
        string target = Write("result.yaml", "keep me");
        ToolRun run = await new WsdlOpenApiTool(new WsdlOpenApiExporter(), ConsoleFor(path, target))
            .RunAsync(CancellationToken.None);
        run.Outcome.Should().Be(RunOutcome.Failed);
        (await File.ReadAllTextAsync(target)).Should().Be("keep me");
    }

    private static TestConsole ConsoleFor(string path, string target)
    {
        TestConsole console = new();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter(path);
        console.Input.PushTextWithEnter(target);
        console.Input.PushTextWithEnter("SOAP");
        console.Input.PushTextWithEnter("1.0");
        return console;
    }

    [Fact]
    public async Task Both_SOAP_versions_can_expose_the_same_portType_and_endpoint()
    {
        string path = Contract();
        string copy = await File.ReadAllTextAsync(Path.Combine(_directory, "binding.wsdl"));
        Write("soap12.wsdl", copy.Replace("urn:binding", "urn:soap12", StringComparison.Ordinal)
            .Replace("http://schemas.xmlsoap.org/wsdl/soap/", "http://schemas.xmlsoap.org/wsdl/soap12/", StringComparison.Ordinal));
        await Replace(path, "<service", "<import namespace='urn:soap12' location='soap12.wsdl'/><service");
        await Replace(path, "</service>", "<port name='Soap12' binding='s:Shared' xmlns:s='urn:soap12' xmlns:s12='http://schemas.xmlsoap.org/wsdl/soap12/'><s12:address location='https://private.example/actual'/></port></service>");
        YamlMappingNode document = Parse(await Export(path));
        YamlMappingNode content = Map(document, "paths", "/actual", "post", "requestBody", "content");
        content.Children.Should().ContainKey(new YamlScalarNode("text/xml"))
            .And.ContainKey(new YamlScalarNode("application/soap+xml"));
        Map(document, "paths").Children.Should().ContainSingle();
    }

    [Fact]
    public async Task Recursive_types_keep_resolvable_component_references()
    {
        string path = Contract();
        await Replace(Path.Combine(_directory, "types.xsd"), "name='count' type='xs:int'", "name='count' type='tns:Payload' minOccurs='0'");
        string yaml = await Export(path);
        yaml.Should().Contain("#/components/schemas/XmlType1");
        Map(Parse(yaml), "components", "schemas").ToString().Should().Contain("count");
    }

    [Fact]
    public async Task DTD_and_external_entities_are_rejected_before_projection()
    {
        string path = Contract();
        await Replace(path, "<definitions", "<!DOCTYPE definitions [<!ENTITY stolen SYSTEM 'file:///etc/passwd'>]><definitions");
        await Refused(path, "XML_DTD_OR_PARSE_INVALID");
    }

    [Fact]
    public async Task XSD_traversal_is_refused_without_partial_schema_export()
    {
        string path = Contract();
        await Replace(Path.Combine(_directory, "model.wsdl"), "schemaLocation='types.xsd'", "schemaLocation='../types.xsd'");
        await Refused(path, "WSDL_XSD_GRAPH_UNRESOLVED_OR_INVALID");
    }

    private string Contract(string soap = "http://schemas.xmlsoap.org/wsdl/soap/")
    {
        Write("types.xsd", """
            <xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:tns='urn:types' targetNamespace='urn:types' elementFormDefault='qualified'>
              <xs:complexType name='Payload'><xs:sequence>
                <xs:element name='count' type='xs:int'/>
                <xs:element name='label' type='xs:string' minOccurs='0' maxOccurs='3'/>
                <xs:element name='nested'><xs:complexType><xs:sequence><xs:element name='value' type='xs:string'/></xs:sequence></xs:complexType></xs:element>
              </xs:sequence></xs:complexType>
              <xs:element name='Request' type='tns:Payload'/><xs:element name='Reply' type='xs:string'/><xs:element name='Failure' type='xs:string'/>
            </xs:schema>
            """);
        Write("model.wsdl", """
            <definitions xmlns='http://schemas.xmlsoap.org/wsdl/' xmlns:tns='urn:model' xmlns:data='urn:types' targetNamespace='urn:model'>
              <types><xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:import namespace='urn:types' schemaLocation='types.xsd'/></xs:schema></types>
              <message name='RequestMessage'><part name='body' element='data:Request'/></message>
              <message name='ReplyMessage'><part name='body' element='data:Reply'/></message>
              <message name='FaultMessage'><part name='detail' element='data:Failure'/></message>
              <portType name='Port'><operation name='Call'><input message='tns:RequestMessage'/><output message='tns:ReplyMessage'/><fault name='Failed' message='tns:FaultMessage'/></operation></portType>
            </definitions>
            """);
        Write("binding.wsdl", $$"""
            <definitions xmlns='http://schemas.xmlsoap.org/wsdl/' xmlns:m='urn:model' xmlns:soap='{{soap}}' targetNamespace='urn:binding'>
              <import namespace='urn:model' location='model.wsdl'/>
              <binding name='Shared' type='m:Port'><soap:binding style='document' transport='http://schemas.xmlsoap.org/soap/http'/>
                <operation name='Call'><soap:operation soapAction=''/><input><soap:body use='literal'/></input><output><soap:body use='literal'/></output><fault name='Failed'><soap:fault name='Failed' use='literal'/></fault></operation>
              </binding>
            </definitions>
            """);
        return Write("service.wsdl", $$"""
            <definitions xmlns='http://schemas.xmlsoap.org/wsdl/' xmlns:b='urn:binding' xmlns:soap='{{soap}}' targetNamespace='urn:service'>
              <import namespace='urn:binding' location='binding.wsdl'/>
              <service name='Service'><port name='Port' binding='b:Shared'><soap:address location='https://private.example/actual'/></port></service>
            </definitions>
            """);
    }

    private string Write(string name, string contents)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static async Task Replace(string path, string old, string replacement) =>
        await File.WriteAllTextAsync(path, (await File.ReadAllTextAsync(path)).Replace(old, replacement, StringComparison.Ordinal));

    private static Task<string> Export(string path) =>
        SoapOpenApiYamlExporter.ExportFileAsync(path, "SOAP", "1.0", CancellationToken.None);

    private static Task<FluentAssertions.Specialized.ExceptionAssertions<WsdlExportException>> Refused(string path, string reason)
    {
        Func<Task> export = async () => _ = await Export(path);
        return export.Should().ThrowAsync<WsdlExportException>().WithMessage("*" + reason + "*");
    }

    private static YamlMappingNode Parse(string yaml)
    {
        YamlStream stream = new();
        using StringReader reader = new(yaml);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents.Single().RootNode;
    }

    private static YamlMappingNode Map(YamlMappingNode node, params string[] names)
    {
        foreach (string name in names)
        {
            node = (YamlMappingNode)node.Children[new YamlScalarNode(name)];
        }

        return node;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
