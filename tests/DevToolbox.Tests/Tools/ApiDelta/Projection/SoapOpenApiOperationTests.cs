using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

public sealed class SoapOpenApiOperationTests
{
    [Theory]
    [InlineData(SoapVersion.Soap11, "text/xml", "http://schemas.xmlsoap.org/soap/envelope/")]
    [InlineData(SoapVersion.Soap12, "application/soap+xml", "http://www.w3.org/2003/05/soap-envelope")]
    [InlineData(SoapVersion.None, null, null)]
    public void Soap_operation_preserves_protocol_specific_media_and_envelope(
        SoapVersion version, string? mediaType, string? envelopeNamespace)
    {
        SoapOpenApiOperation.HttpMethod.Should().Be("post");
        SoapOpenApiOperation.RequestMediaType(version).Should().Be(mediaType);
        SoapOpenApiOperation.EnvelopeNamespace(version).Should().Be(envelopeNamespace);
    }
}
