using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

public sealed class DraftWriterYamlScalarTests
{
    [Theory]
    [InlineData("#/components/schemas/Order")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("12.5")]
    [InlineData("- item")]
    [InlineData("a: b")]
    [InlineData("hello # comment")]
    [InlineData("")]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    [InlineData("0x2a")]
    [InlineData(".inf")]
    [InlineData("2026-11-01")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("line\rbreak")]
    [InlineData("line\u2028break")]
    public void Ambiguous_yaml_text_is_quoted(string value)
    {
        DraftWriter.Text(value, SpecPointer.Root).Style
            .Should().Be(ScalarStyle.DoubleQuoted);
    }

    [Fact]
    public void Ordinary_component_names_remain_plain()
    {
        DraftWriter.Text("CustomerAddress", SpecPointer.Root).Style
            .Should().Be(ScalarStyle.Plain);
    }
}
