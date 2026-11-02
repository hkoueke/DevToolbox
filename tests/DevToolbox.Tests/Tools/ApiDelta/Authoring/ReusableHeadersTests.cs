using System.Text;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Authoring;

/// <summary>
/// Les en-têtes qu'une production déclare sont-ils trouvés, comptés et ordonnés ?
/// </summary>
/// <remarks>
/// C'est l'asymétrie de cette fonctionnalité : le contrat de service ne porte pas ces en-têtes, la
/// production les déclare. L'outil les propose donc parce qu'il les a lus chez le développeur, et jamais
/// parce qu'il les devine — d'où le dernier essai, qui établit qu'une production sans en-tête n'en reçoit
/// aucun.
/// </remarks>
public sealed class ReusableHeadersTests
{
    [Fact]
    public void The_headers_of_a_real_production_are_found_counted_and_ordered()
    {
        SpecDocument production = Read("Authoring.rpc-production.yaml");

        IReadOnlyList<OfferedHeader> offered = ReusableHeaders.In(production);

        offered.Select(one => one.Component).Should().BeEquivalentTo(
            ["X-idCorrelation", "X-serviceEmetteur", "X-serviceInitial", "X-utilisateur", "X-MARCHE"],
            "les cinq en-têtes du corpus, et eux seuls");

        offered.Should().OnlyContain(
            one => one.Routes == 4, "les quatre routes du corpus les portent toutes");

        offered.Select(one => one.Routes).Should().BeInDescendingOrder(
            "le plus référencé d'abord : c'est l'ordre dans lequel ils sont offerts");
    }

    [Fact]
    public void A_header_carries_its_wire_name_and_its_reference()
    {
        OfferedHeader header = ReusableHeaders.In(Read("Authoring.rpc-production.yaml"))
            .Single(one => string.Equals(one.Component, "X-utilisateur", StringComparison.Ordinal));

        header.Name.Should().Be("X-utilisateur");
        header.Reference.Should().Be("#/components/parameters/X-utilisateur");
        header.Display.Should().Contain("X-utilisateur").And.Contain("4");
    }

    [Fact]
    public void Only_headers_are_offered()
    {
        // Un composant de chemin ou de requête appartient aux routes qui le définissent : le proposer
        // inviterait à attacher à une route neuve un paramètre qui n'y veut rien dire.
        SpecDocument production = Read("baseline-30.yaml");

        IReadOnlyList<OfferedHeader> offered = ReusableHeaders.In(production);

        foreach (OfferedHeader header in offered)
        {
            SpecNode? declared = PointerNavigator.Find(
                production.Root, "/components/parameters/" + header.Component + "/in");

            declared.Should().BeOfType<ScalarNode>().Which.Text.Should().Be("header");
        }
    }

    [Fact]
    public void A_production_declaring_no_reusable_header_is_offered_nothing()
    {
        // « cyclic.yaml » ne déclare aucun composant de paramètre. Le premier choix de cet essai —
        // « candidate-additive.yaml » — en déclarait un, ce que l'essai a montré du premier coup : une
        // hypothèse sur le corpus vaut d'être vérifiée plutôt que supposée.
        SpecDocument production = Read("cyclic.yaml");

        ReusableHeaders.In(production).Should().BeEmpty("rien n'est inventé");
    }

    [Fact]
    public void A_unanimous_usage_is_distinguished_from_a_partial_one()
    {
        // Un usage unanime est une convention qu'on suit sauf raison contraire ; un usage partiel est un
        // choix qu'on fait route par route. C'est ce qui décide si les cases sont cochées par défaut.
        IReadOnlyList<OfferedHeader> all = ReusableHeaders.In(Read("Authoring.rpc-production.yaml"));

        ReusableHeaders.Unanimous(all, routes: 4).Should().BeTrue();
        ReusableHeaders.Unanimous(all, routes: 5).Should().BeFalse("une route ne les porte pas");
        ReusableHeaders.Unanimous([], routes: 4).Should().BeFalse("rien d'offert n'est pas unanime");
    }

    private static SpecDocument Read(string fileName, DocumentRole role = DocumentRole.Production)
    {
        SpecReadResult result = new SpecReader(NullLogger<SpecReader>.Instance).ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read(fileName)), fileName, role, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            fileName + " doit se lire : "
            + string.Join(" / ", result.Diagnostics.Select(one => one.Message)));

        return result.Document;
    }
}
