using DevToolbox.Tools.ApiDelta.Core.Projection;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

/// <summary>Cas limites de conformité des références de composants OpenAPI.</summary>
public sealed class SchemaReferenceConformanceTests
{
    private static readonly string[] ReorderedNames =
    [
        "urn:fournisseurs:Adresse",
        "urn:clients:Adresse",
        "urn:clients:Adresse",
    ];

    [Theory]
    [InlineData("urn:example:Type/Version", "#/components/schemas/Type~1Version")]
    [InlineData("urn:example:Type~Version", "#/components/schemas/Type~0Version")]
    [InlineData("urn:example:Type~/Version", "#/components/schemas/Type~0~1Version")]
    public void Schema_references_escape_json_pointer_tokens(
        string qualifiedName, string expectedReference)
    {
        SchemaNameRegistry registry = new([qualifiedName]);

        registry.Reference(qualifiedName).Should().Be(expectedReference);
    }

    [Fact]
    public void Namespace_collisions_remain_stable_with_duplicate_input_names()
    {
        string[] names =
        [
            "urn:clients:Adresse",
            "urn:fournisseurs:Adresse",
            "urn:clients:Adresse",
        ];

        SchemaNameRegistry registry = new(names);
        SchemaNameRegistry reordered = new(ReorderedNames);

        registry.Reference("urn:clients:Adresse")
            .Should().Be(reordered.Reference("urn:clients:Adresse"));
        registry.Reference("urn:fournisseurs:Adresse")
            .Should().Be(reordered.Reference("urn:fournisseurs:Adresse"));
        registry.Key("urn:clients:Adresse")
            .Should().NotBe(registry.Key("urn:fournisseurs:Adresse"));
    }
}
