using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class SoapHttpSemanticsTests
{
    [Theory]
    [InlineData(SoapVersion.Soap11, "text/xml", true)]
    [InlineData(SoapVersion.Soap12, "application/soap+xml", false)]
    [InlineData(SoapVersion.None, null, false)]
    public void Soap_versions_have_distinct_http_semantics(
        SoapVersion version, string? expectedMediaType, bool expectedHeader)
    {
        SoapHttpSemantics.MediaType(version).Should().Be(expectedMediaType);
        SoapHttpSemantics.UsesSoapActionHeader(version).Should().Be(expectedHeader);
    }
}
