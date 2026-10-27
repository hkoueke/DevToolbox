
using DevToolbox.Tools.ApiDelta.Core.Projection;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Projection;

/// <summary>
/// Le jeu de règles couvre-t-il la table publiée, exactement ?
/// </summary>
/// <remarks>
/// Cet essai lit <c>contracts/projection.md</c> et le compare au code. Il existe parce qu'une table de
/// correspondance qui cesse discrètement de décrire l'outil est pire qu'une absence de table : quelqu'un
/// s'y fiera, et ce quelqu'un décidera d'une mise en production sur ce qu'il y a lu.
/// <para>
/// Les deux sens sont vérifiés. Une ligne sans règle est une promesse non tenue ; une règle sans ligne est
/// un comportement que personne n'a publié, et dont un lecteur du contrat ne peut pas se douter.
/// </para>
/// </remarks>
public sealed class ProjectionTableTests
{
    [Fact]
    public void Every_published_row_has_exactly_one_rule()
    {
        IReadOnlyList<PublishedRow> published = Published();

        published.Should().HaveCountGreaterThan(20, "la table publiée a été trouvée et lue");

        foreach (PublishedRow row in published)
        {
            IProjectionRule rule = ProjectionTable.Rules
                .Should().ContainSingle(candidate =>
                    string.Equals(candidate.Source, row.Source, StringComparison.Ordinal),
                    "la ligne « " + row.Source + " » doit avoir une règle, et une seule")
                .Subject;

            rule.Target.Should().Be(row.Target, "la deuxième colonne est recopiée mot pour mot");
            rule.Confidence.Should().Be(row.Confidence, "pour la ligne « " + row.Source + " »");
        }
    }

    [Fact]
    public void Every_rule_answers_to_a_published_row()
    {
        IReadOnlyList<string> published = [.. Published().Select(row => row.Source)];

        foreach (IProjectionRule rule in ProjectionTable.Rules)
        {
            published.Should().Contain(
                rule.Source,
                "aucune règle ne doit décrire un comportement que la table ne publie pas");
        }
    }

    [Fact]
    public void The_rule_set_and_the_table_have_the_same_size()
    {
        // Les deux essais précédents passeraient encore si une ligne était publiée deux fois, ou si deux
        // règles portaient la même ligne. Celui-ci ferme ce passage.
        ProjectionTable.Rules.Should().HaveCount(Published().Count);

        ProjectionTable.Rules.Select(rule => rule.Source).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void A_rule_that_does_not_carry_always_names_a_reason()
    {
        // Le motif est ce par quoi les écarts se groupent. Une règle approchante ou absente sans motif
        // produirait un écart impossible à grouper, donc une ligne par occurrence — exactement ce que la
        // fonctionnalité promet d'éviter.
        foreach (IProjectionRule rule in ProjectionTable.Rules
            .Where(rule => rule.Confidence != ProjectionConfidence.Carried))
        {
            rule.Reason.Should().NotBeNull("pour la ligne « " + rule.Source + " »");
        }
    }

    [Fact]
    public void A_rule_that_carries_never_names_a_reason()
    {
        // Et l'inverse, qui compte autant : un motif sur une règle portée laisserait croire à un écart
        // là où il n'y en a pas.
        foreach (IProjectionRule rule in ProjectionTable.Rules
            .Where(rule => rule.Confidence == ProjectionConfidence.Carried))
        {
            rule.Reason.Should().BeNull("pour la ligne « " + rule.Source + " »");
        }
    }

    [Fact]
    public void The_three_confidences_are_all_represented()
    {
        // Si une seule confiance subsistait, la projection serait soit assurée partout soit douteuse
        // partout, et dans les deux cas elle n'enseignerait plus rien.
        ProjectionTable.Rules.Select(rule => rule.Confidence).Distinct()
            .Should().BeEquivalentTo(Enum.GetValues<ProjectionConfidence>());
    }

    /// <summary>Lit les lignes de la table publiée.</summary>
    /// <remarks>
    /// Le fichier est lu depuis le dépôt, et non embarqué : le propre de cet essai est de parler du
    /// document que les gens ouvrent. Une copie embarquée pourrait dériver de l'original, ce qui est
    /// précisément le défaut qu'il s'agit d'empêcher.
    /// </remarks>
    private static List<PublishedRow> Published()
    {
        string path = Path.Combine(
            Repository(), "specs", "004-wsdl-delta-audit", "contracts", "projection.md");

        File.Exists(path).Should().BeTrue("la table publiée doit se trouver à « " + path + " »");

        List<PublishedRow> rows = [];

        foreach (string line in File.ReadLines(path))
        {
            if (Row(line) is { } row)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>Lit une ligne de la table, ou rend rien si ce n'en est pas une.</summary>
    private static PublishedRow? Row(string line)
    {
        string[] cells = [.. line.Split('|', StringSplitOptions.TrimEntries)];

        // Une ligne de la table de projection a six cellules : les quatre colonnes encadrées de deux
        // vides. La table de dérivation d'itinéraire en a autant, mais sa troisième colonne n'est pas une
        // confiance, ce qui suffit à l'écarter sans la nommer.
        //
        // La cellule de confiance est écrite entre accents graves dans le document publié, qui est du
        // Markdown avant d'être une table : les retirer ici est moins fragile que de demander au
        // rédacteur de s'en passer.
        if (cells.Length != 6
            || !Enum.TryParse(cells[3].Trim('`'), out ProjectionConfidence confidence))
        {
            return null;
        }

        return new PublishedRow(cells[1], cells[2], confidence);
    }

    /// <summary>La racine du dépôt, trouvée en remontant depuis le binaire.</summary>
    private static string Repository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DevToolbox.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("le dépôt doit être trouvable depuis le binaire des essais");

        return directory.FullName;
    }

    private sealed record PublishedRow(string Source, string Target, ProjectionConfidence Confidence);
}
