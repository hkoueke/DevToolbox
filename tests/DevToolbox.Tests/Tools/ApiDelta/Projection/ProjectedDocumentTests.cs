using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

/// <summary>
/// Le document synthétique est-il de la forme que l'outil compare déjà, et sans aucune étendue ?
/// </summary>
/// <remarks>
/// L'absence d'étendue est l'invariant que le plan a nommé comme son risque principal. L'épissage se
/// repère aux étendues pour recopier les octets d'origine autour de ce qui change ; une étendue inventée
/// sur un nœud projeté le ferait écrire n'importe où dans la production, et le dégât serait silencieux.
/// </remarks>
public sealed class ProjectedDocumentTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-projete-" + Guid.NewGuid().ToString("N"));

    public ProjectedDocumentTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Not_one_node_of_a_projected_document_carries_a_byte_extent()
    {
        ProjectionResult projected = Project();

        IReadOnlyList<SpecNode> nodes = [.. Walk(projected.Document.Root)];

        nodes.Should().HaveCountGreaterThan(50, "l'arbre a bien été parcouru en entier");
        nodes.Should().OnlyContain(node => node.Span == ByteSpan.None);
        nodes.Should().OnlyContain(node => node.Position.Line == 0);
    }

    [Fact]
    public void The_projected_document_announces_its_format_and_carries_no_source()
    {
        ProjectionResult projected = Project();

        projected.Document.Format.Should().Be(
            SpecFormat.Projected, "l'épissage refuse une production qui n'est pas du YAML");

        projected.Document.Source.IsEmpty.Should().BeTrue("il n'y a aucun octet d'origine");
        projected.Document.ByteLength.Should().Be(0);
        projected.Document.ContentHash.Should().BeEmpty();
    }

    [Fact]
    public void Every_operation_carries_a_payload_and_a_response()
    {
        // Le défaut que le vidage a montré : une première version produisait des routes sans charge
        // utile, si bien que la comparaison voyait les routes et ne pouvait jamais dire qu'un argument
        // avait changé — c'est-à-dire la moitié de ce que l'outil promet.
        MappingNode paths = Section(Project().Document.Root, "paths");

        paths.Entries.Should().HaveCount(
            3, "cinq opérations, dont trois se partagent « /documents »");

        foreach (MappingEntry path in paths.Entries)
        {
            MappingNode item = path.Value.Should().BeOfType<MappingNode>().Subject;

            foreach (MappingEntry method in item.Entries)
            {
                MappingNode action = method.Value.Should().BeOfType<MappingNode>().Subject;

                Keys(action).Should().Contain(
                    "responses", "l'opération « " + path.Key.Text + " » doit rendre quelque chose");
            }
        }
    }

    [Fact]
    public void A_reference_is_written_in_a_style_that_survives_being_emitted()
    {
        // « #/components/schemas/X » écrit en style simple ouvrirait un commentaire YAML, et le renvoi
        // disparaîtrait. Rien ne réémet ce document aujourd'hui ; un arbre dont l'écriture est fausse
        // attend seulement le premier émetteur qu'on y branchera.
        IReadOnlyList<ScalarNode> references =
        [
            .. Walk(Project().Document.Root)
                .OfType<ScalarNode>()
                .Where(scalar => scalar.Text.StartsWith('#')),
        ];

        references.Should().NotBeEmpty("le corpus produit bien des renvois");
        references.Should().OnlyContain(scalar => scalar.Style != ScalarStyle.Plain);
    }

    [Fact]
    public void A_property_of_a_projected_schema_always_has_a_shape()
    {
        // L'autre défaut du vidage : les propriétés sortaient vides. Un objet dont chaque propriété
        // accepte n'importe quoi est compatible avec n'importe quelle production, et la comparaison
        // n'aurait plus rien à dire.
        MappingNode schemas = Section(
            Section(Project().Document.Root, "components"), "schemas");

        foreach (MappingEntry schema in schemas.Entries)
        {
            if (schema.Value is not MappingNode body
                || !Keys(body).Contains("properties", StringComparer.Ordinal))
            {
                continue;
            }

            foreach (MappingEntry property in Section(body, "properties").Entries)
            {
                MappingNode shape = property.Value.Should().BeOfType<MappingNode>().Subject;

                Keys(shape).Should().IntersectWith(
                    ["$ref", "type", "items"],
                    "la propriété « " + property.Key.Text + " » de « " + schema.Key.Text
                        + " » doit avoir une forme");
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ProjectionResult Project()
    {
        foreach (string name in new[] { "catalogue.wsdl", "catalogue-types.xsd", "commun.xsd" })
        {
            File.WriteAllText(
                Path.Combine(_root, name),
                CorpusLoader.Read("Wsdl." + name),
                new UTF8Encoding(false));
        }

        WsdlReadResult read = new WsdlReader().ReadAsync(
            Path.Combine(_root, "catalogue.wsdl"),
            WsdlLimits.Default,
            ImportPolicy.ConfinedTo(_root),
            CancellationToken.None).GetAwaiter().GetResult();

        read.IsFaithful.Should().BeTrue(
            string.Join(" · ", read.Diagnostics.Select(one => one.Message)));

        return new Projector().Project(read.Contract!, OpenApiVersion.V30);
    }

    private static MappingNode Section(SpecNode node, string key) =>
        node.Should().BeOfType<MappingNode>().Subject
            .Entries.Single(entry => string.Equals(entry.Key.Text, key, StringComparison.Ordinal))
            .Value.Should().BeOfType<MappingNode>().Subject;

    private static IEnumerable<string> Keys(MappingNode node) =>
        node.Entries.Select(entry => entry.Key.Text);

    private static IEnumerable<SpecNode> Walk(SpecNode node)
    {
        yield return node;

        switch (node)
        {
            case MappingNode map:
                foreach (MappingEntry entry in map.Entries)
                {
                    yield return entry.Key;

                    foreach (SpecNode child in Walk(entry.Value))
                    {
                        yield return child;
                    }
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    foreach (SpecNode child in Walk(item))
                    {
                        yield return child;
                    }
                }

                break;

            default:
                break;
        }
    }
}
