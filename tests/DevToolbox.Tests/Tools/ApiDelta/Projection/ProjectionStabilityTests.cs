using System.Globalization;
using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tests.Tools.ApiDelta.Wsdl;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

/// <summary>
/// Trois livraisons du même contrat, écrites autrement : disent-elles la même chose ?
/// </summary>
/// <remarks>
/// La promesse est celle sur laquelle une équipe s'appuie sans y penser : le jour où le fournisseur
/// renomme ses préfixes ou regroupe ses schémas dans un seul fichier, l'outil ne doit pas annoncer que le
/// contrat a changé. Un faux positif, ici, coûte une matinée à chaque livraison.
/// <para>
/// Ce que les trois livraisons ne peuvent pas partager, ce sont les noms de fichiers et les positions :
/// une trace d'origine désigne la livraison qu'on a réellement lue, et en désigner une autre serait faux.
/// La comparaison des documents engendrés les neutralise donc, comme celle des artefacts neutralise le
/// bloc de métadonnées d'une exécution.
/// </para>
/// </remarks>
public sealed class ProjectionStabilityTests
{
    [Fact]
    public void Three_deliveries_of_one_contract_project_the_very_same_tree()
    {
        using WsdlCorrectionRun ordinary = WsdlCorrectionRun.Establish();
        using WsdlCorrectionRun inlined = Inlined();
        using WsdlCorrectionRun renamed = Renamed();

        string expected = Render(ordinary.Projection.Document.Root);

        expected.Should().Contain("/paths/~1documents/get", "l'arbre a bien été rendu");

        Render(inlined.Projection.Document.Root).Should().Be(
            expected, "les schémas écrits dans le WSDL décrivent le même contrat");

        Render(renamed.Projection.Document.Root).Should().Be(
            expected, "d'autres préfixes et un autre ordre de déclarations décrivent le même contrat");
    }

    [Fact]
    public void They_leave_the_same_things_behind_and_count_them_the_same_way()
    {
        using WsdlCorrectionRun ordinary = WsdlCorrectionRun.Establish();
        using WsdlCorrectionRun inlined = Inlined();
        using WsdlCorrectionRun renamed = Renamed();

        IReadOnlyList<string> expected = Said(ordinary.Projection);

        expected.Should().NotBeEmpty();

        Said(inlined.Projection).Should().Equal(expected);
        Said(renamed.Projection).Should().Equal(expected);

        ordinary.Projection.Figures.Should().Be(inlined.Projection.Figures);
        ordinary.Projection.Figures.Should().Be(renamed.Projection.Figures);
    }

    [Fact]
    public void They_cover_production_identically()
    {
        using WsdlCorrectionRun ordinary = WsdlCorrectionRun.Establish();
        using WsdlCorrectionRun inlined = Inlined();
        using WsdlCorrectionRun renamed = Renamed();

        IReadOnlyList<string> expected = Covered(ordinary.Footing.Coverage);

        expected.Should().HaveCount(5, "le catalogue porte cinq opérations");

        Covered(inlined.Footing.Coverage).Should().Equal(expected);
        Covered(renamed.Footing.Coverage).Should().Equal(expected);
    }

    [Fact]
    public void Under_the_same_selection_they_generate_the_same_document()
    {
        using WsdlCorrectionRun ordinary = WsdlCorrectionRun.Establish();
        using WsdlCorrectionRun inlined = Inlined();
        using WsdlCorrectionRun renamed = Renamed();

        string expected = Generated(ordinary);

        expected.Should().Contain("/modifies", "le document engendré porte bien la route retenue");

        Generated(inlined).Should().Be(expected);
        Generated(renamed).Should().Be(expected);
    }

    [Fact]
    public void Each_delivery_still_names_the_documents_it_was_actually_read_from()
    {
        // Le revers de l'épreuve précédente, et il compte autant : neutraliser l'origine pour comparer
        // ne doit pas vouloir dire que l'origine est interchangeable. Une trace qui nommerait le même
        // fichier pour trois livraisons différentes serait fausse pour deux d'entre elles.
        using WsdlCorrectionRun inlined = Inlined();

        Result<BuiltCorrection> built = inlined.Build(Chosen(inlined));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string text = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        text.Should().Contain("wsdl: catalogue-inline.wsdl");
        text.Should().NotContain("catalogue-types.xsd", "cette livraison n'a pas lu ce fichier");
    }

    [Fact]
    public void A_sequence_keeps_its_order_and_a_choice_does_not_have_one()
    {
        // Les deux moitiés de WR-103, et elles se contredisent assez pour mériter d'être éprouvées
        // ensemble : l'ordre d'une séquence fait partie du contrat, celui des branches d'un choix non.
        // Les membres d'un objet JSON ne sont pas ordonnés — porter l'ordre d'écriture d'un choix
        // ferait différer deux livraisons qui disent la même chose.
        using WsdlCorrectionRun run = WsdlCorrectionRun.Establish();

        MappingNode schemas = Section(
            Section(run.Projection.Document.Root, "components"), "schemas");

        Keys(Section(Section(schemas, "Document"), "properties")).Should().StartWith(
            ["id", "titre", "etat"],
            "l'ordre de la séquence est celui du contrat, et un lecteur doit l'y retrouver");

        SequenceNode branches = Section(schemas, "Critere")
            .Entries.Single(entry => string.Equals(entry.Key.Text, "oneOf", StringComparison.Ordinal))
            .Value.Should().BeOfType<SequenceNode>().Subject;

        IEnumerable<string> chosen = branches.Items
            .Select(branch => Keys(Section((MappingNode)branch, "properties")).Single());

        chosen.Should().Equal(
            ["parEtat", "parId", "parTitre"],
            "les branches sortent par nom, et le contrat les écrit dans un autre ordre");
    }

    private static MappingNode Section(SpecNode node, string key) =>
        node.Should().BeOfType<MappingNode>().Subject
            .Entries.Single(entry => string.Equals(entry.Key.Text, key, StringComparison.Ordinal))
            .Value.Should().BeOfType<MappingNode>().Subject;

    private static IEnumerable<string> Keys(MappingNode node) =>
        node.Entries.Select(entry => entry.Key.Text);

    private static WsdlCorrectionRun Inlined() =>
        WsdlCorrectionRun.Delivered("catalogue-inline.wsdl", "catalogue-inline.wsdl");

    private static WsdlCorrectionRun Renamed() => WsdlCorrectionRun.Delivered(
        "catalogue-prefixes.wsdl", "catalogue-prefixes.wsdl", "catalogue-types.xsd", "commun.xsd");

    /// <summary>La même sélection pour les trois : une route neuve, et une différence sur une couverte.</summary>
    private static CorrectionSelection Chosen(WsdlCorrectionRun run) =>
        new([run.Route("dossierModifie")]);

    /// <summary>Le document engendré, ce qui nomme la livraison mis à part.</summary>
    private static string Generated(WsdlCorrectionRun run)
    {
        Result<BuiltCorrection> built = run.Build(Chosen(run));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        string text = Encoding.UTF8.GetString(built.Value.Serialized.Span);

        return string.Join('\n', text.Split('\n').Select(Anonymised));
    }

    /// <summary>Une ligne, ce qui nomme la livraison remplacé par ce qu'elle dit toutes.</summary>
    private static string Anonymised(string line)
    {
        string trimmed = line.TrimStart();

        foreach (string naming in new[] { "wsdl: ", "emplacement: " })
        {
            if (trimmed.StartsWith(naming, StringComparison.Ordinal))
            {
                return line[..(line.Length - trimmed.Length)] + naming + "«la livraison lue»";
            }
        }

        return line;
    }

    /// <summary>Ce que la projection a laissé derrière, sans l'endroit où elle l'a laissé.</summary>
    /// <remarks>
    /// L'emplacement est écarté parce qu'il change d'une livraison à l'autre — et il le doit : un type
    /// écrit dans le WSDL n'est pas à la même ligne du même fichier qu'un type écrit dans un schéma à
    /// part. Ce qui doit coïncider, c'est <em>ce</em> que la projection n'a pas porté, et non d'où.
    /// </remarks>
    private static IReadOnlyList<string> Said(ProjectionResult projection) =>
    [
        .. projection.Gaps.Select(gap =>
            gap.Reason + " | " + gap.Confidence + " | " + gap.Construct + " | " + gap.Subject),
    ];

    private static IReadOnlyList<string> Covered(CoverageReport coverage) =>
    [
        .. coverage.Entries
            .OrderBy(entry => entry.Operation, StringComparer.Ordinal)
            .Select(entry => entry.Operation + " | " + entry.State + " | " + entry.Route),
    ];

    /// <summary>L'arbre, rendu ligne à ligne : l'ordre y est visible, et une différence y est lisible.</summary>
    private static string Render(SpecNode node)
    {
        StringBuilder builder = new();

        Walk(node, builder);

        return builder.ToString();
    }

    private static void Walk(SpecNode node, StringBuilder builder)
    {
        switch (node)
        {
            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    _ = builder.Append(entry.Key.Location.Json).Append(" {").AppendLine(entry.Key.Text);
                    Walk(entry.Value, builder);
                }

                break;

            case SequenceNode sequence:
                for (int index = 0; index < sequence.Items.Count; index++)
                {
                    _ = builder.Append('[')
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("]");

                    Walk(sequence.Items[index], builder);
                }

                break;

            case ScalarNode scalar:
                _ = builder.Append(scalar.Location.Json).Append(" = ").AppendLine(scalar.Text);

                break;

            default:
                break;
        }
    }
}
