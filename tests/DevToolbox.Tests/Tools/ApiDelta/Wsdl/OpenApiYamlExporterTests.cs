using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Yaml;
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

        string yaml = Encoding.UTF8.GetString(OpenApiYamlExporter.Export(draft).Span);

        yaml.Should().Contain("openapi: 3.0.3");
        yaml.Should().Contain("$ref: '#/components/schemas/SoapBody'");
        yaml.Should().Contain("title: SOAP API");
    }

    [Theory]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("FALSE")]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("~")]
    [InlineData("12.5")]
    [InlineData("001")]
    [InlineData("0x2a")]
    [InlineData(".inf")]
    [InlineData("2026-11-01")]
    [InlineData("no")]
    [InlineData("#/components/schemas/Order")]
    [InlineData("- item")]
    [InlineData("? key")]
    [InlineData("a: b")]
    [InlineData("hello # comment")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("quote ' and \" backslash \\")]
    [InlineData("first\nsecond")]
    [InlineData("first\rsecond")]
    [InlineData("first\r\nsecond")]
    [InlineData("first\tsecond")]
    [InlineData("first\0second")]
    [InlineData("first\u001fsecond")]
    [InlineData("first\u0085second")]
    [InlineData("first\u2028second")]
    [InlineData("first\u2029second")]
    [InlineData("Été 😀")]
    public void Text_keys_and_values_round_trip_without_changing_types_or_characters(string value)
    {
        SchemaDraft draft = new();
        draft.Set("text", value);
        draft.Set("flag", true);
        draft.Set("number", 12.5m);
        draft.Set("nothing", null);
        // Empty/whitespace keys are outside SchemaDraft's API; all other keys share the scalar writer.
        if (!string.IsNullOrWhiteSpace(value))
        {
            draft.Section("keys").Set(value, "present");
        }

        SpecNode reread = Parse(OpenApiYamlExporter.Export(draft));

        ScalarNode text = (ScalarNode)PointerNavigator.Find(reread, "/text")!;
        text.Text.Should().Be(value);
        text.Resolved.Kind.Should().Be(ScalarValueKind.Text);
        ((ScalarNode)PointerNavigator.Find(reread, "/flag")!).Resolved.Kind.Should().Be(ScalarValueKind.Boolean);
        ((ScalarNode)PointerNavigator.Find(reread, "/number")!).Resolved.Canonical.Should().Be("12.5");
        ((ScalarNode)PointerNavigator.Find(reread, "/nothing")!).Resolved.Kind.Should().Be(ScalarValueKind.Null);
        if (!string.IsNullOrWhiteSpace(value))
        {
            MappingNode keys = (MappingNode)PointerNavigator.Find(reread, "/keys")!;
            keys.Entries.Should().ContainSingle().Which.Key.Text.Should().Be(value);
        }
    }

    [Fact]
    public void Serialized_references_resolve_to_distinct_components_with_escaped_names()
    {
        string[] names = ["urn:clients:Type~/Version", "urn:fournisseurs:Type~/Version"];
        SchemaNameRegistry registry = new(names);
        SchemaDraft draft = new();
        SchemaDraft schemas = draft.Section("components").Section("schemas");
        foreach (string name in names)
        {
            schemas.Section(registry.Key(name)).Set("description", name);
            draft.Append("references", registry.Reference(name));
        }

        SpecNode reread = Parse(OpenApiYamlExporter.Export(draft));
        SequenceNode references = (SequenceNode)PointerNavigator.Find(reread, "/references")!;

        references.Items.Should().HaveCount(2);
        for (int index = 0; index < names.Length; index++)
        {
            string reference = ((ScalarNode)references.Items[index]).Text;
            MappingNode target = (MappingNode)PointerNavigator.Find(reread, reference[1..])!;
            target.Should().NotBeNull();
            ((ScalarNode)target.Find("description")!).Text.Should().Be(names[index]);
        }
    }

    private static SpecNode Parse(ReadOnlyMemory<byte> yaml)
    {
        string text = Encoding.UTF8.GetString(yaml.Span);
        using StringReader reader = new(text);
        YamlTreeBuilder parser = new(new YamlDotNet.Core.Parser(reader), LoadLimits.Default, new ByteOffsets(text, 0));
        SpecNode? result = parser.Build();
        parser.Diagnostics.Should().BeEmpty();
        return result!;
    }
}
