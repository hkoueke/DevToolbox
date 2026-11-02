using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Correction;

/// <summary>
/// Ce que l'épisseur touche, et surtout ce qu'il ne touche pas.
/// </summary>
/// <remarks>
/// La promesse éprouvée ici est celle qui rend une production corrigée relisible : hors des endroits
/// désignés, la sortie est la production, octet pour octet. Un émetteur qui reformaterait au passage
/// produirait un document juste et un diff que personne ne relit.
/// </remarks>
public sealed class SplicingTests
{
    private readonly SplicingEmitter _splicer = new(new CanonicalEmitter());
    private readonly SpecReader _reader = new(NullLogger<SpecReader>.Instance);

    [Fact]
    public void Splicing_nothing_reproduces_the_document_byte_for_byte()
    {
        SpecDocument production = Read("baseline-30.yaml");

        Result<ReadOnlyMemory<byte>> spliced =
            _splicer.Splice(production, [], CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeTrue();
        spliced.Value.ToArray().Should().Equal(
            production.Source.ToArray(), "aucune épissure ne doit rien changer");
    }

    [Fact]
    public void A_replacement_leaves_every_other_byte_untouched()
    {
        SpecDocument production = Read("baseline-30.yaml");
        SpecPointer target = Target(production, "/info/version");

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [new Splice(SpliceKind.Replace, target, Value: Scalar("9.9.9"))],
            CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeTrue(spliced.Failure?.Message);

        string before = Text(production.Source);
        string after = Text(spliced.Value);

        after.Should().Contain("9.9.9");

        // Hors de la ligne touchée, tout doit se retrouver tel quel — commentaires compris.
        string[] originals = before.Split('\n');
        string[] results = after.Split('\n');

        results.Length.Should().Be(originals.Length, "un remplacement de scalaire ne change pas le nombre de lignes");

        int changed = originals.Where((line, at) => !string.Equals(line, results[at], StringComparison.Ordinal)).Count();

        changed.Should().Be(1, "une seule ligne doit différer");
    }

    [Fact]
    public void An_added_route_appears_and_the_document_still_parses()
    {
        SpecDocument production = Read("baseline-30.yaml");
        SpecDocument candidate = Read("candidate-additive.yaml", DocumentRole.Candidate);

        SpecPointer paths = Target(production, "/paths");
        SpecNode added = Node(candidate, "/paths/~1dossiers~1{id}");

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [new Splice(SpliceKind.Add, paths, Key: "/dossiers/{id}", Value: added)],
            CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeTrue(spliced.Failure?.Message);

        SpecReadResult reread = _reader.ReadBytes(
            spliced.Value, "corrigé", DocumentRole.Production, LoadLimits.Default);

        reread.Document.Should().NotBeNull(
            "un document épissé doit se relire : " + string.Join(" / ", reread.Diagnostics.Select(d => d.Message)));

        PointerNavigator.Find(reread.Document.Root, "/paths/~1dossiers~1{id}")
            .Should().NotBeNull("la route ajoutée doit être là");

        // Et ce que la production disait doit toujours y être.
        PointerNavigator.Find(reread.Document.Root, "/paths/~1documents")
            .Should().NotBeNull("rien de la production ne disparaît");
    }

    [Fact]
    public void Everything_the_production_said_survives_an_addition()
    {
        SpecDocument production = Read("baseline-30.yaml");
        SpecDocument candidate = Read("candidate-additive.yaml", DocumentRole.Candidate);

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [
                new Splice(
                    SpliceKind.Add,
                    Target(production, "/paths"),
                    Key: "/dossiers/{id}",
                    Value: Node(candidate, "/paths/~1dossiers~1{id}")),
            ],
            CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeTrue(spliced.Failure?.Message);

        // La production est un préfixe de la sortie, à l'octet près : on n'a inséré qu'après la dernière
        // entrée de « paths », et rien avant n'a été réécrit.
        string before = Text(production.Source);
        string after = Text(spliced.Value);

        foreach (string line in before.Split('\n').Where(line => line.Trim().Length > 0))
        {
            after.Should().Contain(line, "la ligne « " + line.Trim() + " » venait de la production");
        }
    }

    [Fact]
    public void Two_overlapping_changes_are_refused_rather_than_written_over_each_other()
    {
        SpecDocument production = Read("baseline-30.yaml");
        SpecPointer info = Target(production, "/info");

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [
                new Splice(SpliceKind.Replace, info, Value: Scalar("remplacé")),
                new Splice(SpliceKind.Replace, Target(production, "/info/version"), Value: Scalar("9.9.9")),
            ],
            CanonicalFormatting.Default);

        spliced.IsFailure.Should().BeTrue("une épissure dans une autre écrirait l'une par-dessus l'autre");
        spliced.Failure!.Message.Should().Contain("recouvrent");
    }

    [Fact]
    public void A_json_production_is_refused_by_naming_the_reason()
    {
        SpecDocument production = Read("baseline-30.json");

        Result<ReadOnlyMemory<byte>> spliced =
            _splicer.Splice(production, [], CanonicalFormatting.Default);

        spliced.IsFailure.Should().BeTrue();
        spliced.Failure!.Message.Should().Contain("YAML");
    }

    [Fact]
    public void An_absent_target_is_refused_by_naming_it()
    {
        SpecDocument production = Read("baseline-30.yaml");

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [new Splice(SpliceKind.Replace, SpecPointer.Root.Child("inexistant"), Value: Scalar("x"))],
            CanonicalFormatting.Default);

        spliced.IsFailure.Should().BeTrue();
        spliced.Failure!.Message.Should().Contain("inexistant");
    }

    private SpecDocument Read(string fileName, DocumentRole role = DocumentRole.Production)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(CorpusLoader.Read(fileName));
        SpecReadResult result = _reader.ReadBytes(bytes, fileName, role, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            fileName + " doit se lire : "
            + string.Join(" / ", result.Diagnostics.Select(d => d.Message)));

        return result.Document;
    }

    /// <summary>L'emplacement d'un nœud de la production, tel que son arbre le porte.</summary>
    private static SpecPointer Target(SpecDocument document, string json) =>
        (PointerNavigator.Find(document.Root, json)
            ?? throw new InvalidOperationException(json + " absent")).Location;

    private static SpecNode Node(SpecDocument document, string json) =>
        PointerNavigator.Find(document.Root, json)
            ?? throw new InvalidOperationException(json + " absent");

    /// <summary>Un scalaire neuf, celui que l'outil écrirait.</summary>
    [Fact]
    public void An_addition_lands_after_the_named_neighbour_and_the_order_holds()
    {
        // La seule capacité qui manquait à l'épisseur : l'ajout plaçait toute entrée nouvelle après la
        // dernière de l'association. Une description de production est relue par des personnes et ses
        // chemins sont groupés à la main — ajouter toute route nouvelle à la fin dispersait une famille
        // de routes qui se tiennent ensemble.
        SpecDocument production = Read("baseline-30.yaml");
        SpecDocument candidate = Read("candidate-additive.yaml", DocumentRole.Candidate);

        IReadOnlyList<string> before = Paths(production);

        before.Should().HaveCountGreaterThan(1, "il faut un voisin à nommer");

        string neighbour = before[0];

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [
                new Splice(
                    SpliceKind.Add,
                    Target(production, "/paths"),
                    Key: "/dossiers/{id}",
                    Value: Node(candidate, "/paths/~1dossiers~1{id}"),
                    AfterKey: neighbour),
            ],
            CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeTrue(spliced.Failure?.Message);

        SpecReadResult reread = _reader.ReadBytes(
            spliced.Value, "corrigé", DocumentRole.Production, LoadLimits.Default);

        reread.Document.Should().NotBeNull(
            string.Join(" / ", reread.Diagnostics.Select(one => one.Message)));

        IReadOnlyList<string> after = Paths(reread.Document);

        after[1].Should().Be("/dossiers/{id}", "la route doit suivre le voisin nommé");

        // Et l'ordre de ce qui existait est intact : la nouvelle s'insère, elle ne réordonne rien.
        after.Where(path => before.Contains(path, StringComparer.Ordinal))
            .Should().Equal(before, "les chemins existants gardent leur ordre");
    }

    [Fact]
    public void An_addition_after_an_unknown_neighbour_is_refused_rather_than_appended()
    {
        // Rabattre sur la fin mettrait la route ailleurs que là où le développeur l'a demandée, ce qui
        // est exactement ce que ce paramètre existe pour empêcher. Un déplacement silencieux est pire
        // qu'un refus : le refus se corrige, le déplacement se découvre en relisant le fichier.
        SpecDocument production = Read("baseline-30.yaml");
        SpecDocument candidate = Read("candidate-additive.yaml", DocumentRole.Candidate);

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [
                new Splice(
                    SpliceKind.Add,
                    Target(production, "/paths"),
                    Key: "/dossiers/{id}",
                    Value: Node(candidate, "/paths/~1dossiers~1{id}"),
                    AfterKey: "/chemin-qui-n-existe-pas"),
            ],
            CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeFalse("le voisin nommé n'existe pas");
        spliced.Failure!.Reason.Should().Be(FailureReason.InvalidInput);
        spliced.Failure.Message.Should().Contain("/chemin-qui-n-existe-pas");
    }

    [Fact]
    public void An_addition_with_no_named_neighbour_still_lands_at_the_end()
    {
        // Le comportement d'avant, inchangé : c'est ce qui permet au reste de l'outil de ne rien savoir
        // du nouveau paramètre.
        SpecDocument production = Read("baseline-30.yaml");
        SpecDocument candidate = Read("candidate-additive.yaml", DocumentRole.Candidate);

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            production,
            [
                new Splice(
                    SpliceKind.Add,
                    Target(production, "/paths"),
                    Key: "/dossiers/{id}",
                    Value: Node(candidate, "/paths/~1dossiers~1{id}")),
            ],
            CanonicalFormatting.Default);

        spliced.IsSuccess.Should().BeTrue(spliced.Failure?.Message);

        SpecReadResult reread = _reader.ReadBytes(
            spliced.Value, "corrigé", DocumentRole.Production, LoadLimits.Default);

        Paths(reread.Document!)[^1].Should().Be("/dossiers/{id}");
    }

    /// <summary>Les chemins d'un document, dans l'ordre où il les écrit.</summary>
    private static IReadOnlyList<string> Paths(SpecDocument document)
    {
        SpecNode? paths = PointerNavigator.Find(document.Root, "/paths");

        return [.. paths.Should().BeOfType<MappingNode>().Subject.Entries
            .Select(entry => entry.Key.Text)];
    }

    private static ScalarNode Scalar(string text) => TreeEdits.Key(text);

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.UTF8.GetString(bytes.Span);
}
