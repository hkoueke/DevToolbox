using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using FluentAssertions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Correspondence;

/// <summary>
/// Rien ne se compare avant confirmation, et ce que le développeur déclare l'emporte.
/// </summary>
/// <remarks>
/// Le risque que le plan a nommé : aucune convention ne fait le pont entre les deux langages. Une
/// correspondance que l'outil aurait inventée sans la montrer rendrait <em>infalsifiable</em> chaque
/// constat posé dessus — un lecteur n'aurait aucun moyen de savoir que la paire comparée n'était pas la
/// bonne. Ces essais portent donc moins sur la justesse des propositions que sur l'impossibilité de
/// comparer sans accord.
/// </remarks>
public sealed class CorrespondenceTests
{
    private static readonly string[] Operations =
        ["deposerDocument", "lireDocument", "majDocument", "archiverDocument"];

    private static readonly string[] Routes =
        ["POST /documents", "GET /documents/{id}", "PUT /documents/{id}", "PATCH /documents/{id}"];

    [Fact]
    public void Nothing_is_comparable_until_the_correspondence_is_confirmed()
    {
        OperationCorrespondence proposed = new SimilarityProposer().Propose(Operations, Routes);

        proposed.IsConfirmed.Should().BeFalse("l'outil propose, il ne décide pas");
        proposed.Entries.Should().NotBeEmpty("il a bien proposé quelque chose");
        proposed.Comparable.Should().BeEmpty(
            "une comparaison sur une base que personne n'a validée est exactement ce qui est interdit");
    }

    [Fact]
    public void Confirming_turns_proposals_into_confirmations_and_opens_the_comparison()
    {
        OperationCorrespondence confirmed =
            new SimilarityProposer().Propose(Operations, Routes).Confirm();

        confirmed.IsConfirmed.Should().BeTrue();
        confirmed.Comparable.Should().NotBeEmpty();
        confirmed.Comparable.Should().OnlyContain(entry =>
            entry.Origin == CorrespondenceOrigin.Confirmed
            || entry.Origin == CorrespondenceOrigin.Declared);
    }

    [Fact]
    public void Confirming_in_bulk_never_resolves_an_ambiguity()
    {
        // Le seul comportement honnête : confirmer l'ensemble ne peut pas valoir résolution d'une
        // ambiguïté que l'outil a précisément renoncé à trancher.
        OperationCorrespondence confirmed =
            new SimilarityProposer().Propose(Operations, Routes).Confirm();

        CorrespondenceEntry? ambiguous = confirmed.Entries.FirstOrDefault(entry =>
            entry.Origin == CorrespondenceOrigin.Ambiguous);

        ambiguous.Should().NotBeNull("le jeu d'épreuve contient délibérément une quasi-égalité");
        confirmed.Comparable.Should().NotContain(entry =>
            string.Equals(entry.Operation, ambiguous.Operation, StringComparison.Ordinal));
    }

    [Fact]
    public void A_near_tie_is_left_ambiguous_with_its_alternatives_named()
    {
        // « majDocument » n'a pas de verbe reconnu, et « PUT /documents/{id} » comme
        // « PATCH /documents/{id} » partagent son sujet. L'outil doit renoncer, et dire entre quoi.
        OperationCorrespondence proposed = new SimilarityProposer().Propose(Operations, Routes);

        CorrespondenceEntry entry = proposed.For("majDocument")!;

        entry.Origin.Should().Be(CorrespondenceOrigin.Ambiguous);
        entry.Alternatives.Should().NotBeEmpty("le développeur doit voir entre quoi l'outil hésite");
        entry.Mark.Should().Be("? ambiguë");
    }

    [Fact]
    public void A_clear_match_is_proposed_with_the_tokens_that_justified_it()
    {
        OperationCorrespondence proposed = new SimilarityProposer().Propose(Operations, Routes);

        CorrespondenceEntry entry = proposed.For("deposerDocument")!;

        entry.Origin.Should().Be(CorrespondenceOrigin.Proposed);
        entry.Route.Should().Be("POST /documents");
        entry.Basis.Should().Contain("document", "ce qui permet de contredire vite");
        entry.Mark.Should().Be("~ proposée");
    }

    [Fact]
    public void An_operation_with_nothing_resembling_it_is_left_without_a_route()
    {
        OperationCorrespondence proposed = new SimilarityProposer().Propose(
            ["calculerTaxe"], ["GET /documents", "POST /dossiers"]);

        CorrespondenceEntry entry = proposed.For("calculerTaxe")!;

        entry.HasRoute.Should().BeFalse();
        entry.Mark.Should().Be("∅ absente", "rien n'est cassé : quelque chose manque");
    }

    [Fact]
    public void A_declared_mapping_wins_over_any_proposal()
    {
        // Une équipe qui a écrit ce fichier a tranché. L'outil ne la discute pas.
        Dictionary<string, string> declared = new(StringComparer.Ordinal)
        {
            ["deposerDocument"] = "PUT /archives",
        };

        CorrespondenceEntry entry = new SimilarityProposer()
            .Propose(Operations, Routes, declared)
            .For("deposerDocument")!;

        entry.Origin.Should().Be(CorrespondenceOrigin.Declared);
        entry.Route.Should().Be("PUT /archives", "et non la proposition, qui était POST /documents");
        entry.Mark.Should().Be("✓ déclarée");
        entry.IsInferred.Should().BeFalse("elle vient du développeur, pas de l'outil");
    }

    [Fact]
    public void A_deliberately_wrong_declared_mapping_is_honoured_and_stays_marked_as_declared()
    {
        // Le risque que le plan a nommé en propre. L'outil doit obéir — c'est la décision du
        // développeur — mais l'origine doit rester visible jusqu'au rapport, parce qu'une correspondance
        // fausse ne produit pas un constat faux ici ou là : elle produit un rapport assuré sur la
        // mauvaise paire.
        Dictionary<string, string> absurde = new(StringComparer.Ordinal)
        {
            ["archiverDocument"] = "GET /documents/{id}",
        };

        OperationCorrespondence confirmed = new SimilarityProposer()
            .Propose(Operations, Routes, absurde)
            .Confirm();

        CorrespondenceEntry entry = confirmed.For("archiverDocument")!;

        entry.Route.Should().Be("GET /documents/{id}", "la déclaration du développeur est honorée");
        entry.Origin.Should().Be(
            CorrespondenceOrigin.Declared, "confirmer n'efface pas d'où l'appariement vient");

        confirmed.Comparable.Should().Contain(candidate =>
            string.Equals(candidate.Operation, "archiverDocument", StringComparison.Ordinal));
    }

    [Fact]
    public void A_proposal_is_the_same_on_two_runs_over_the_same_inputs()
    {
        // Déterminisme : une proposition qui changerait d'une séance à l'autre ferait qu'une reprise ne
        // porterait plus sur ce que le développeur avait confirmé.
        SimilarityProposer proposer = new();

        OperationCorrespondence first = proposer.Propose(Operations, Routes);
        OperationCorrespondence second = proposer.Propose(Operations, [.. Routes.Reverse()]);

        second.Entries.Select(entry => entry.Route)
            .Should().Equal(first.Entries.Select(entry => entry.Route),
                "l'ordre dans lequel les routes arrivent ne doit rien changer");
    }

    [Fact]
    public void Every_operation_receives_exactly_one_entry()
    {
        OperationCorrespondence proposed = new SimilarityProposer().Propose(Operations, Routes);

        proposed.Entries.Should().HaveCount(Operations.Length);
        proposed.Entries.Select(entry => entry.Operation).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Declining_leaves_a_correspondence_that_compares_nothing_and_is_not_a_failure()
    {
        // Renoncer est une réponse. Une séance qui s'arrête ici réussit, n'ayant rien comparé, et le
        // rapport dit que la correspondance n'a pas été confirmée. Traiter le renoncement comme un échec
        // pousserait un développeur à confirmer ce qu'il n'a pas lu.
        OperationCorrespondence declined = OperationCorrespondence.None;

        declined.IsConfirmed.Should().BeFalse();
        declined.Comparable.Should().BeEmpty();
        declined.Entries.Should().BeEmpty();
    }
}
