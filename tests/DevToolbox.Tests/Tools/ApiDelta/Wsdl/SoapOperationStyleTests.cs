using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class SoapOperationStyleTests
{
    [Theory]
    [InlineData("rpc", "document")]
    [InlineData("document", "rpc")]
    public void Operation_style_overrides_binding_default(string defaultStyle, string operationStyle)
    {
        string wsdl = $$"""
            <wsdl:definitions xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/"
                xmlns:soap="http://schemas.xmlsoap.org/wsdl/soap/"
                xmlns:tns="urn:example" targetNamespace="urn:example">
              <wsdl:message name="Request"/>
              <wsdl:portType name="PortType">
                <wsdl:operation name="Execute">
                  <wsdl:input message="tns:Request"/>
                </wsdl:operation>
              </wsdl:portType>
              <wsdl:binding name="Binding" type="tns:PortType">
                <soap:binding style="{{defaultStyle}}" transport="http://schemas.xmlsoap.org/soap/http"/>
                <wsdl:operation name="Execute">
                  <soap:operation style="{{operationStyle}}" soapAction="urn:execute"/>
                  <wsdl:input><soap:body use="literal"/></wsdl:input>
                </wsdl:operation>
              </wsdl:binding>
            </wsdl:definitions>
            """;

        WsdlReadResult result = new WsdlReader().ReadBytes(
            Encoding.UTF8.GetBytes(wsdl), "binding.wsdl",
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(Path.GetTempPath()));

        result.IsFaithful.Should().BeTrue(
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        result.Contract!.Operations.Single().Binding!.Style.Should().Be(operationStyle);
    }
}
