using System.Xml;
using System.Xml.Schema;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Corpus;

/// <summary>
/// Le corpus WSDL est-il bien ce qu'il prétend être ?
/// </summary>
/// <remarks>
/// Écrit avant le lecteur, et éprouvé avant lui aussi. Un corpus que personne n'a vérifié transforme
/// chaque échec ultérieur en enquête : on ne sait plus si c'est le lecteur qui se trompe ou le document
/// qui est faux. Ces essais établissent donc le minimum — les ressources sont atteignables, les documents
/// ordinaires sont bien formés et compilent, les documents hostiles sont bien hostiles de la façon
/// annoncée, et le générateur engendre ce qu'il dit.
/// </remarks>
public sealed class WsdlCorpusTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "devtoolbox-corpus-wsdl-" + Guid.NewGuid().ToString("N"));

    public WsdlCorpusTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("Wsdl.catalogue.wsdl")]
    [InlineData("Wsdl.catalogue-types.xsd")]
    [InlineData("Wsdl.commun.xsd")]
    [InlineData("Wsdl.production.yaml")]
    [InlineData("Wsdl.correspondance.yaml")]
    [InlineData("Wsdl.hostile.doctype.wsdl")]
    [InlineData("Wsdl.hostile.billion-laughs.wsdl")]
    [InlineData("Wsdl.hostile.traversal.wsdl")]
    [InlineData("Wsdl.hostile.cycle.wsdl")]
    [InlineData("Wsdl.hostile.cycle-a.xsd")]
    [InlineData("Wsdl.hostile.cycle-b.xsd")]
    [InlineData("Wsdl.hostile.same-name.wsdl")]
    [InlineData("Wsdl.hostile.same_name.a.types.xsd")]
    [InlineData("Wsdl.hostile.same_name.b.types.xsd")]
    public void Every_corpus_document_is_reachable(string name)
    {
        // Une ressource non embarquée ne se voit qu'au premier essai qui la lit, et le message parle alors
        // d'un fichier absent plutôt que d'un projet mal réglé.
        CorpusLoader.Read(name).Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Wsdl.catalogue.wsdl")]
    [InlineData("Wsdl.catalogue-types.xsd")]
    [InlineData("Wsdl.commun.xsd")]
    [InlineData("Wsdl.hostile.traversal.wsdl")]
    [InlineData("Wsdl.hostile.cycle.wsdl")]
    [InlineData("Wsdl.hostile.same-name.wsdl")]
    public void The_documents_that_should_parse_do_parse(string name)
    {
        Action read = () =>
        {
            using XmlReader reader = XmlReader.Create(
                new StringReader(CorpusLoader.Read(name)),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

            int nodes = 0;

            while (reader.Read())
            {
                nodes++;
            }

            nodes.Should().BeGreaterThan(0, "un document du corpus porte des nœuds");
        };

        read.Should().NotThrow();
    }

    [Theory]
    [InlineData("Wsdl.hostile.doctype.wsdl")]
    [InlineData("Wsdl.hostile.billion-laughs.wsdl")]
    public void The_documents_carrying_a_document_type_definition_are_refused_by_the_parser(string name)
    {
        // Ce que cet essai établit n'est pas que l'outil refuse — il n'existe pas encore — mais que le
        // document est bien hostile de la façon annoncée. Un « doctype.wsdl » sans DOCTYPE serait un
        // essai qui passe pour rien, et c'est le genre de corpus qui endort une suite entière.
        Action read = () =>
        {
            using XmlReader reader = XmlReader.Create(
                new StringReader(CorpusLoader.Read(name)),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

            int nodes = 0;

            while (reader.Read())
            {
                nodes++;
            }

            nodes.Should().BeGreaterThan(0, "un document du corpus porte des nœuds");
        };

        read.Should().Throw<XmlException>("une définition de type de document doit être refusée");
    }

    [Fact]
    public void The_ordinary_schema_set_compiles_and_carries_what_the_projection_will_read()
    {
        XmlSchemaSet set = Compile("catalogue-types.xsd", "commun.xsd");

        set.GlobalTypes.Count.Should().BeGreaterThan(8, "les types du catalogue et ceux du commun");
        set.GlobalElements.Count.Should().BeGreaterThan(8, "les éléments de message et le groupe de substitution");
    }

    [Fact]
    public void The_cycle_compiles_rather_than_looping()
    {
        // Un cycle d'imports est légal. S'il ne compilait pas, l'essai du lecteur ne prouverait rien :
        // il échouerait pour la mauvaise raison.
        XmlSchemaSet set = Compile("hostile/cycle-a.xsd", "hostile/cycle-b.xsd");

        set.GlobalTypes.Count.Should().BeGreaterThan(1, "les deux types du cycle");
    }

    [Fact]
    public void The_generated_chain_is_as_long_as_asked_and_each_link_parses()
    {
        string entry = GeneratedWsdl.Chain(Path.Combine(_root, "chaine"), 400);

        File.Exists(entry).Should().BeTrue();
        Directory.GetFiles(Path.GetDirectoryName(entry)!, "maillon-*.xsd").Should().HaveCount(400);

        // Le dernier maillon ne doit rien importer, sans quoi la chaîne ne se termine pas.
        string last = File.ReadAllText(Path.Combine(Path.GetDirectoryName(entry)!, "maillon-0400.xsd"));

        last.Should().NotContain("xs:import");
        Parses(File.ReadAllText(entry)).Should().BeTrue("le document d'entrée doit se lire");
        Parses(last).Should().BeTrue("le dernier maillon doit se lire");
    }

    [Fact]
    public void The_generated_enumeration_carries_the_values_it_promises()
    {
        string schema = GeneratedWsdl.EnormousEnumeration(20_000);

        Parses(schema).Should().BeTrue();
        Occurrences(schema, "<xs:enumeration").Should().Be(20_000);
    }

    [Fact]
    public void The_generated_attribute_payload_carries_more_than_four_hundred_attributes()
    {
        // Quatre cents, parce que c'est le seuil au-delà duquel un constat par occurrence devient
        // illisible : c'est ce que le groupement doit ramener à une ligne.
        string contract = GeneratedWsdl.ManyAttributes(420);

        Parses(contract).Should().BeTrue();
        Occurrences(contract, "<xs:attribute ").Should().Be(420);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Compile un jeu de schémas depuis le corpus embarqué, en écrivant d'abord sur le disque.</summary>
    /// <remarks>
    /// Sur le disque, parce qu'un import se résout par <c>schemaLocation</c> : le compiler depuis des
    /// chaînes en mémoire éprouverait autre chose que ce que le lecteur fera.
    /// </remarks>
    private XmlSchemaSet Compile(params string[] relativePaths)
    {
        string directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);

        string? first = null;

        foreach (string relative in relativePaths)
        {
            string name = Path.GetFileName(relative);
            string path = Path.Combine(directory, name);

            File.WriteAllText(path, CorpusLoader.Read("Wsdl." + relative.Replace('/', '.')));

            first ??= path;
        }

        List<string> complaints = [];
        XmlSchemaSet set = new();

        // Sans résolveur, XmlSchemaSet ne suit aucun « schemaLocation » : le défaut est nul depuis
        // .NET Core. L'essai en pose un, borné aux fichiers qu'il vient lui-même d'écrire.
        set.XmlResolver = new XmlUrlResolver();
        set.ValidationEventHandler += (_, argument) => complaints.Add(argument.Message);
        _ = set.Add(null, first!);
        set.Compile();

        complaints.Should().BeEmpty("le corpus doit compiler : " + string.Join(" · ", complaints));

        return set;
    }

    private static bool Parses(string xml)
    {
        try
        {
            using XmlReader reader = XmlReader.Create(
                new StringReader(xml),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

            int nodes = 0;

            while (reader.Read())
            {
                nodes++;
            }

            return nodes > 0;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static int Occurrences(string text, string needle)
    {
        int count = 0;

        for (int at = text.IndexOf(needle, StringComparison.Ordinal);
            at >= 0;
            at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
