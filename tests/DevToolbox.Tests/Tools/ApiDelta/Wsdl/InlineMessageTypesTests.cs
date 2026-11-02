using System.Text;
using System.Xml.Linq;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Wsdl;

public sealed class InlineMessageTypesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "devtoolbox-inline-" + Guid.NewGuid().ToString("N"));
    private string Entry => Path.Combine(_root, "anonymous.wsdl");

    public InlineMessageTypesTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Entry, CorpusLoader.Read("Wsdl.anonymous-messages.wsdl"), new UTF8Encoding(false));
    }

    [Fact]
    public async Task Anonymous_messages_and_nested_types_keep_their_fields_facets_and_occurrences_without_name_collisions()
    {
        WsdlReadResult read = await Read();
        read.IsFaithful.Should().BeTrue(string.Join(" / ", read.Diagnostics.Select(diagnostic => diagnostic.Message)));
        WsdlContract contract = read.Contract!;
        string inline = contract.Elements["urn:anonymous-test:Request"];
        string named = contract.Elements["urn:anonymous-test:NamedRequest"];
        inline.Should().NotBe(named);
        contract.Types[named].Particles.Should().ContainSingle().Which.Name.Should().Be("namedOnly");
        XsdType request = contract.Types[inline];
        request.Name.Should().Be(inline);
        request.Particles.Select(particle => particle.Name).Should().Equal("filter", "children");
        XsdParticle filter = request.Particles[0];
        contract.Types[filter.TypeName].Content.Should().Be(XsdContentKind.Simple);
        contract.Types[filter.TypeName].Facets.Where(facet => string.Equals(facet.Kind, "enumeration", StringComparison.Ordinal))
            .Select(facet => facet.Value).Should().Equal("one", "two");
        XsdParticle children = request.Particles[1];
        children.MinOccurs.Should().Be(0);
        children.MaxOccurs.Should().BeNull();
        children.Nillable.Should().BeTrue();
        contract.Types[children.TypeName].Particles.Should().ContainSingle().Which.TypeName.Should().EndWith(":int");
        var fields = InputFields.Of(contract, contract.Operations.Single());
        fields[0].IsComposite.Should().BeFalse();
        fields[1].IsComposite.Should().BeTrue();
        contract.Types[contract.Elements["urn:anonymous-test:Response"]].Particles.Should().ContainSingle().Which.Name.Should().Be("accepted");
    }

    [Fact]
    public async Task Anonymous_type_names_are_stable_when_global_declarations_are_reordered()
    {
        WsdlReadResult first = await Read();
        XDocument xml = XDocument.Load(Entry);
        XElement schema = xml.Descendants(XName.Get("schema", "http://www.w3.org/2001/XMLSchema")).Single();
        var elements = schema.Elements().Reverse().ToArray();
        schema.RemoveNodes();
        schema.Add(elements);
        xml.Save(Entry);

        WsdlReadResult second = await Read();

        second.IsFaithful.Should().BeTrue();
        second.Contract!.Types.Keys.Should().Equal(first.Contract!.Types.Keys);
        second.Contract.Elements.Should().BeEquivalentTo(first.Contract.Elements);
        foreach (string key in first.Contract.Types.Keys)
        {
            second.Contract.Types[key].Particles.Select(particle => particle.TypeName)
                .Should().Equal(first.Contract.Types[key].Particles.Select(particle => particle.TypeName));
        }
    }

    [Fact]
    public async Task The_enumeration_budget_also_applies_to_anonymous_simple_types()
    {
        WsdlReadResult read = await Read(WsdlLimits.Default with { MaxEnumerationValues = 1 });

        read.IsFaithful.Should().BeFalse();
        read.Diagnostics.Should().Contain(diagnostic => string.Equals(diagnostic.Code, DiagnosticCodes.LimitReached, StringComparison.Ordinal));
    }

    private Task<WsdlReadResult> Read(WsdlLimits? limits = null) => new WsdlReader().ReadAsync(
        Entry, limits ?? WsdlLimits.Default, ImportPolicy.ConfinedTo(_root), CancellationToken.None);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
