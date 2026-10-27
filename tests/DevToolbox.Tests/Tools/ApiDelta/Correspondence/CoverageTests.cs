using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Correspondence;

/// <summary>
/// Chaque opération porte-t-elle exactement un état, et la couverture reste-t-elle séparée du reste ?
/// </summary>
/// <remarks>
/// La couverture et la compatibilité répondent à deux questions, portent deux certitudes, et sont
/// traitées par deux personnes. Les mêler ferait apparaître un manque parmi les ruptures : rien n'est
/// cassé quand une opération n'a pas de route — quelque chose est absent, et les deux nouvelles ne
/// s'adressent pas au même lecteur ni au même calendrier.
/// </remarks>
public sealed class CoverageTests
{
    private static readonly string[] Operations =
        ["deposerDocument", "lireDocument", "majDocument", "calculerTaxe"];

    private static readonly HashSet<string> Production = new(StringComparer.Ordinal)
    {
        "POST /documents",
        "GET /documents/{id}",
        "PUT /documents/{id}",
        "PATCH /documents/{id}",
    };

    [Fact]
    public void Nothing_is_established_while_the_correspondence_is_unconfirmed()
    {
        // Un rapport sans entrée n'annonce pas une couverture totale : il n'a rien établi. Rendre « tout
        // absent » serait alarmant et faux ; « tout couvert » serait rassurant et faux.
        CoverageReport report = CoverageReport.Establish(
            Proposed(), Operations, Production);

        report.IsEstablished.Should().BeFalse();
        report.Entries.Should().BeEmpty();
        report.Gaps.Should().BeEmpty("y compris les manques, qu'on ne peut pas encore affirmer");
    }

    [Fact]
    public void Every_operation_carries_exactly_one_state()
    {
        CoverageReport report = Established();

        report.Entries.Should().HaveCount(Operations.Length);
        report.Entries.Select(entry => entry.Operation).Should().OnlyHaveUniqueItems();

        // Les trois familles sont disjointes et couvrent l'ensemble : un état, et un seul.
        (report.Covered.Count() + report.Gaps.Count() + report.Ambiguous.Count())
            .Should().Be(report.Entries.Count);
    }

    [Fact]
    public void An_operation_production_answers_is_covered()
    {
        CoverageEntry entry = Established().Entries
            .Single(candidate => string.Equals(
                candidate.Operation, "deposerDocument", StringComparison.Ordinal));

        entry.State.Should().Be(CoverageState.Covered);
        entry.Route.Should().Be("POST /documents");
        entry.Remedy.Should().BeNull("il n'y a rien à faire");
    }

    [Fact]
    public void An_operation_nothing_answers_is_absent_and_carries_what_to_do()
    {
        CoverageEntry entry = Established().Entries
            .Single(candidate => string.Equals(
                candidate.Operation, "calculerTaxe", StringComparison.Ordinal));

        entry.State.Should().Be(CoverageState.Absent);
        entry.Remedy.Should().NotBeNull("un rapport dont on ne peut pas partir est inutilisable");
        // « POST /taxes » : le sujet se dégage, le verbe « calculer » n'est pas dans la table. La
        // dérivation est partielle, et c'est la bonne réponse — « /calculertaxes » serait le repli d'un
        // nom d'un seul mot.
        entry.Remedy.Should().Contain("calculerTaxe").And.Contain("POST /taxes");
    }

    [Fact]
    public void An_ambiguous_pairing_stays_ambiguous_and_names_the_candidates()
    {
        CoverageEntry entry = Established().Entries
            .Single(candidate => string.Equals(
                candidate.Operation, "majDocument", StringComparison.Ordinal));

        entry.State.Should().Be(CoverageState.Ambiguous);
        entry.Remedy.Should().Contain("Plusieurs routes");
        entry.Remedy.Should().Contain("/documents", "les candidats sont nommés");
    }

    [Fact]
    public void A_declared_route_production_does_not_carry_is_a_gap_and_says_so()
    {
        // Le développeur a déclaré une route que la production ne porte pas. Ce n'est pas une rupture :
        // c'est un manque, et le message doit dire laquelle des deux choses il constate.
        Dictionary<string, string> declared = new(StringComparer.Ordinal)
        {
            ["lireDocument"] = "GET /archives/{id}",
        };

        CoverageReport report = CoverageReport.Establish(
            new SimilarityProposer().Propose(Operations, [.. Production], declared).Confirm(),
            Operations,
            Production);

        CoverageEntry entry = report.Entries
            .Single(candidate => string.Equals(
                candidate.Operation, "lireDocument", StringComparison.Ordinal));

        entry.State.Should().Be(CoverageState.Absent);
        entry.Remedy.Should().Contain("GET /archives/{id}")
            .And.Contain("que la production ne porte pas");
    }

    [Fact]
    public void No_coverage_gap_is_ever_phrased_as_a_breaking_change()
    {
        // La formulation est le fond, ici. Un manque annoncé comme une rupture ferait reporter une mise
        // en production qui n'avait rien à craindre.
        CoverageReport report = Established();

        foreach (CoverageEntry gap in report.Gaps)
        {
            gap.Remedy!.Should().NotContainAny("rupture", "cassé", "incompatible", "régression");
            gap.Remedy.Should().Contain("devrait porter", "ce qui manque est dit comme un manque");
        }
    }

    [Fact]
    public void The_coverage_of_the_corpus_matches_what_the_corpus_documents()
    {
        // Le corpus a été écrit pour que la couverture soit vérifiable contre une attente écrite : deux
        // opérations couvertes, une ambiguë, une absente.
        CoverageReport report = Established();

        report.Covered.Select(entry => entry.Operation)
            .Should().BeEquivalentTo("deposerDocument", "lireDocument");

        report.Ambiguous.Select(entry => entry.Operation)
            .Should().BeEquivalentTo("majDocument");

        report.Gaps.Select(entry => entry.Operation)
            .Should().BeEquivalentTo("calculerTaxe");
    }

    [Fact]
    public void Coverage_rests_on_the_confirmed_correspondence_and_on_nothing_else()
    {
        // Une correspondance confirmée vers une production vide rend tout absent — et aucune comparaison
        // n'a eu lieu pour l'établir. C'est ce qui rend la couverture certaine là où la compatibilité ne
        // l'est pas.
        CoverageReport report = CoverageReport.Establish(
            Proposed().Confirm(), Operations, new HashSet<string>(StringComparer.Ordinal));

        report.IsEstablished.Should().BeTrue();
        report.Covered.Should().BeEmpty();
        report.Gaps.Should().HaveCount(3, "l'ambiguë reste ambiguë, elle n'est pas devenue absente");
        report.Ambiguous.Should().HaveCount(1);
    }

    private static OperationCorrespondence Proposed() =>
        new SimilarityProposer().Propose(Operations, [.. Production]);

    private static CoverageReport Established() =>
        CoverageReport.Establish(Proposed().Confirm(), Operations, Production);
}
