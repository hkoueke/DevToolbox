using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Authoring;

public sealed class OperationAssemblyTests
{
    [Fact]
    public void Mixed_placements_filter_the_body_preserve_field_schemas_and_leave_shared_components_intact()
    {
        var (root, operation) = Projected();
        AuthoredRoute route = Route([
            new("count", "xs:int", false, ParameterLocation.Path, false),
            new("enabled", "xs:boolean", false, ParameterLocation.Query, false),
            new("code", "urn:types:Code", false, ParameterLocation.Header, true, "X-Code"),
            new("label", "xs:string", false, ParameterLocation.Body, false),
            new("child", "urn:types:Child", true, ParameterLocation.Body, true),
            new("discarded", "xs:string", false, ParameterLocation.Nowhere, false),
        ]);

        SpecNode composed = OperationAssembly.Compose(operation, route, [], root);

        MappingNode properties = At<MappingNode>(composed, "/requestBody/content/application~1json/schema/properties");
        properties.Entries.Select(entry => entry.Key.Text).Should().Equal("label", "child");
        At<SequenceNode>(composed, "/requestBody/content/application~1json/schema/required").Items
            .Cast<ScalarNode>().Select(node => node.Text).Should().Equal("child");
        At<ScalarNode>(composed, "/requestBody/required").Text.Should().Be("true");
        At<ScalarNode>(composed, "/parameters/0/schema/type").Text.Should().Be("integer");
        At<ScalarNode>(composed, "/parameters/0/schema/format").Text.Should().Be("int32");
        At<ScalarNode>(composed, "/parameters/0/schema/minimum").Text.Should().Be("1");
        At<ScalarNode>(composed, "/parameters/0/schema/xml/name").Text.Should().Be("count");
        At<ScalarNode>(composed, "/parameters/0/required").Text.Should().Be("true");
        At<ScalarNode>(composed, "/parameters/1/schema/type").Text.Should().Be("boolean");
        At<ScalarNode>(composed, "/parameters/2/name").Text.Should().Be("X-Code");
        At<ScalarNode>(composed, "/parameters/2/schema/$ref").Text.Should().Be("#/components/schemas/Code");
        At<MappingNode>(root, "/components/schemas/Input/properties").Entries.Should().HaveCount(6);
        At<SequenceNode>(root, "/components/schemas/Input/required").Items.Should().HaveCount(2);
        At<ScalarNode>(operation, "/requestBody/content/application~1json/schema/$ref").Text.Should().Be("#/components/schemas/Input");
        At<MappingNode>(composed, "/responses").Should().BeSameAs(operation.Find("responses"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Requiredness_is_applied_even_when_every_field_stays_in_the_body(bool required)
    {
        var (root, operation) = Projected();
        AuthoredRoute route = Route([new("label", "xs:string", false, ParameterLocation.Body, required)]);

        SpecNode composed = OperationAssembly.Compose(operation, route, [], root);

        At<ScalarNode>(composed, "/requestBody/required").Text.Should().Be(required ? "true" : "false");
        MappingNode schema = At<MappingNode>(composed, "/requestBody/content/application~1json/schema");
        At<MappingNode>(schema, "/properties").Entries.Should().ContainSingle().Which.Key.Text.Should().Be("label");
        (schema.Find("required") is SequenceNode).Should().Be(required);
        At<ScalarNode>(schema, "/xml/name").Text.Should().Be("Input");
        At<ScalarNode>(schema, "/additionalProperties").Text.Should().Be("false");
    }

    [Theory]
    [InlineData(ParameterLocation.Path)]
    [InlineData(ParameterLocation.Query)]
    [InlineData(ParameterLocation.Header)]
    public void Projected_scalar_constraints_take_precedence_over_a_legacy_declared_type(ParameterLocation location)
    {
        var (root, operation) = Projected();
        SpecNode composed = OperationAssembly.Compose(operation,
            Route([new("count", "xs:string", false, location, false)]), [], root);

        At<ScalarNode>(composed, "/parameters/0/schema/type").Text.Should().Be("integer");
        At<ScalarNode>(composed, "/parameters/0/schema/maximum").Text.Should().Be("99");
        PointerNavigator.Find(composed, "/requestBody").Should().BeNull();
    }

    [Theory]
    [InlineData("xs:int", "integer", "int32")]
    [InlineData("http://www.w3.org/2001/XMLSchema:boolean", "boolean", null)]
    [InlineData("xs:dateTime", "string", "date-time")]
    public void Builtin_declarations_are_mapped_without_a_default_to_string(string declared, string type, string? format)
    {
        var (root, operation) = Projected();
        SpecNode composed = OperationAssembly.Compose(operation,
            Route([new("legacy", declared, false, ParameterLocation.Query, false)]), [], root);

        At<ScalarNode>(composed, "/parameters/0/schema/type").Text.Should().Be(type);
        string? actualFormat = (PointerNavigator.Find(composed, "/parameters/0/schema/format") as ScalarNode)?.Text;
        actualFormat.Should().Be(format);
    }

    [Fact]
    public void An_unknown_qualified_type_is_not_invented_from_its_local_name()
    {
        var (root, operation) = Projected();
        Action compose = () => OperationAssembly.Compose(operation,
            Route([new("legacy", "urn:custom:string", false, ParameterLocation.Query, false)]), [], root);

        compose.Should().Throw<InvalidOperationException>().WithMessage("*legacy*");
    }

    [Fact]
    public void Reusing_a_header_does_not_remove_an_undecided_body()
    {
        var (root, operation) = Projected();
        SpecNode composed = OperationAssembly.Compose(operation, Route([]), ["Trace"], root);

        PointerNavigator.Find(composed, "/requestBody").Should().BeSameAs(operation.Find("requestBody"));
        At<ScalarNode>(composed, "/parameters/0/$ref").Text.Should().Be("#/components/parameters/Trace");
    }

    [Theory]
    [InlineData("oneOf")]
    [InlineData("anyOf")]
    [InlineData("allOf")]
    public void Whole_body_compositions_keep_their_branches_constraints_and_conditional_requiredness(string keyword)
    {
        var (root, operation) = Composed(keyword);
        SpecNode composed = OperationAssembly.Compose(operation, Route([
            new("left", "xs:string", false, ParameterLocation.Body, false),
            new("right", "xs:int", false, ParameterLocation.Body, false),
        ]), [], root);

        const string Schema = "/requestBody/content/application~1json/schema";
        At<SequenceNode>(composed, Schema + "/" + keyword).Items.Should().HaveCount(2);
        At<ScalarNode>(composed, Schema + "/" + keyword + "/0/required/0").Text.Should().Be("left");
        At<ScalarNode>(composed, Schema + "/" + keyword + "/1/required/0").Text.Should().Be("right");
        At<ScalarNode>(composed, Schema + "/" + keyword + "/1/properties/right/minimum").Text.Should().Be("1");
        PointerNavigator.Find(composed, Schema + "/required").Should().BeNull("les obligations conditionnelles restent dans les branches");
        At<ScalarNode>(composed, "/requestBody/required").Text.Should().Be("false");
        At<SequenceNode>(root, "/components/schemas/Input/" + keyword).Items.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(ParameterLocation.Path)]
    [InlineData(ParameterLocation.Query)]
    [InlineData(ParameterLocation.Header)]
    [InlineData(ParameterLocation.Nowhere)]
    public void Splitting_a_composition_is_refused_instead_of_flattened(ParameterLocation location)
    {
        var (root, operation) = Composed("oneOf");
        Action compose = () => OperationAssembly.Compose(operation, Route([
            new("left", "xs:string", false, location, false),
            new("right", "xs:int", false, ParameterLocation.Body, false),
        ]), [], root);

        compose.Should().Throw<InvalidOperationException>().WithMessage("*composition*corps*");
    }

    [Fact]
    public void Moving_every_composed_field_out_of_the_body_is_also_refused()
    {
        var (root, operation) = Composed("oneOf");
        Action compose = () => OperationAssembly.Compose(operation, Route([
            new("left", "xs:string", false, ParameterLocation.Query, false),
            new("right", "xs:int", false, ParameterLocation.Query, false),
        ]), [], root);

        compose.Should().Throw<InvalidOperationException>().WithMessage("*composition*");
    }

    private static (SpecNode Root, MappingNode Operation) Composed(string keyword)
    {
        SchemaDraft draft = new();
        SchemaDraft input = draft.Section("components").Section("schemas").Section("Input");
        SchemaDraft left = new();
        left.Property("left").Set("type", "string");
        left.Append("required", "left");
        SchemaDraft right = new();
        right.Property("right").Set("type", "integer");
        right.Property("right").Set("minimum", 1);
        right.Append("required", "right");
        input.Append(keyword, left);
        input.Append(keyword, right);
        SchemaDraft operation = draft.Section("paths").Section("/source").Section("post");
        operation.Section("requestBody").Section("content").Section("application/json").Section("schema")
            .Set("$ref", "#/components/schemas/Input");
        SpecNode root = DraftWriter.Write(draft, SpecPointer.Root);
        return (root, At<MappingNode>(root, "/paths/~1source/post"));
    }

    private static (SpecNode Root, MappingNode Operation) Projected()
    {
        SchemaDraft draft = new();
        SchemaDraft schemas = draft.Section("components").Section("schemas");
        SchemaDraft input = schemas.Section("Input");
        input.Set("type", "object");
        input.Set("additionalProperties", false);
        input.Section("xml").Set("name", "Input");
        input.Property("count").Set("type", "integer");
        input.Property("count").Set("format", "int32");
        input.Property("count").Set("minimum", 1);
        input.Property("count").Set("maximum", 99);
        input.Property("count").Section("xml").Set("name", "count");
        input.Property("enabled").Set("type", "boolean");
        input.Property("code").Set("$ref", "#/components/schemas/Code");
        input.Property("label").Set("type", "string");
        input.Property("child").Set("$ref", "#/components/schemas/Child");
        input.Property("discarded").Set("type", "string");
        input.Append("required", "count");
        input.Append("required", "label");
        schemas.Section("Code").Set("type", "string");
        schemas.Section("Code").Append("enum", "AA");
        schemas.Section("Child").Set("type", "object");
        SchemaDraft operation = draft.Section("paths").Section("/source").Section("post");
        operation.Section("requestBody").Set("required", true);
        operation.Section("requestBody").Section("content").Section("application/json").Section("schema")
            .Set("$ref", "#/components/schemas/Input");
        operation.Section("responses").Section("202").Set("description", "Accepted");
        SpecNode root = DraftWriter.Write(draft, SpecPointer.Root);
        return (root, At<MappingNode>(root, "/paths/~1source/post"));
    }

    private static AuthoredRoute Route(IReadOnlyList<FieldPlacement> fields) => new("Create", "/created/{count}",
        HttpOperationMethod.Post, fields, [], InsertionPoint.AtEnd, RouteDerivationKind.Fallback, false);

    private static T At<T>(SpecNode root, string pointer) where T : SpecNode =>
        PointerNavigator.Find(root, pointer).Should().BeOfType<T>().Subject;
}
