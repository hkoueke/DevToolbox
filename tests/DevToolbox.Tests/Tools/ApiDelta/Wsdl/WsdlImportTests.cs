using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class WsdlImportTests
{
    [Theory]
    [InlineData("urn:shared", "shared.wsdl", true)]
    [InlineData(null, "shared.wsdl", false)]
    [InlineData("urn:shared", null, false)]
    [InlineData(" ", "shared.wsdl", false)]
    [InlineData("urn:shared", " ", false)]
    public void Import_requires_both_namespace_and_location(
        string? namespaceName, string? location, bool expected)
    {
        new WsdlImport(namespaceName, location)
            .HasRequiredAttributes.Should().Be(expected);
    }
}
