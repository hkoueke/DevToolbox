using DevToolbox.Tools.ApiDelta.Core.Projection;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

public sealed class SchemaNameRegistryTests
{
    [Fact]
    public void Unique_local_names_keep_existing_component_names()
    {
        SchemaNameRegistry registry = new(["urn:clients:Adresse", "urn:clients:Client"]);

        registry.Key("urn:clients:Adresse").Should().Be("Adresse");
        registry.Reference("urn:clients:Client").Should().Be("#/components/schemas/Client");
    }

    [Fact]
    public void Equal_local_names_from_distinct_namespaces_never_collide()
    {
        SchemaNameRegistry registry = new(["urn:clients:Adresse", "urn:fournisseurs:Adresse"]);

        registry.Key("urn:clients:Adresse").Should().NotBe(registry.Key("urn:fournisseurs:Adresse"));
        registry.Reference("urn:clients:Adresse").Should().NotBe(registry.Reference("urn:fournisseurs:Adresse"));
    }

    [Fact]
    public void Mapping_does_not_depend_on_input_order()
    {
        string[] names = ["urn:clients:Adresse", "urn:fournisseurs:Adresse", "urn:clients:Client"];
        SchemaNameRegistry forward = new(names);
        SchemaNameRegistry backward = new(names.Reverse());

        foreach (string name in names)
        {
            forward.Reference(name).Should().Be(backward.Reference(name));
        }
    }

    [Fact]
    public void Local_name_matching_an_encoded_qualified_name_does_not_shadow_another_component()
    {
        const string first = "urn:clients:Adresse";
        const string second = "urn:fournisseurs:Adresse";
        string encoded = "Adresse__" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(first));

        SchemaNameRegistry registry = new([first, second, "urn:other:" + encoded]);

        registry.Key(first).Should().NotBe(registry.Key("urn:other:" + encoded));
        registry.Key(second).Should().NotBe(registry.Key(first));
        registry.Reference(first).Should().Be("#/components/schemas/" + registry.Key(first));
    }

    [Fact]
    public void Component_pointers_escape_json_pointer_special_characters()
    {
        SchemaNameRegistry registry = new(["urn:example:Path/Type~Version"]);

        registry.Reference("urn:example:Path/Type~Version")
            .Should().Be("#/components/schemas/Path~1Type~0Version");
    }
}
