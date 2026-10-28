using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using DevToolbox.Tests.Tools.ApiDelta.Corpus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevToolbox.Tests.Tools.ApiDelta.Correction;

/// <summary>
/// Ce que la vérification laisse publier, et ce qu'elle arrête.
/// </summary>
/// <remarks>
/// La dernière épreuve de ce fichier est celle qui compte : un constructeur délibérément cassé, qui touche
/// une route que personne n'avait retenue. Si la vérification la laissait passer, toute la promesse de ce
/// mode — « le diff que vous lirez est la sélection que vous avez faite » — ne vaudrait rien.
/// </remarks>
public sealed class CorrectionVerifierTests
{
    private readonly SpecReader _reader = new(NullLogger<SpecReader>.Instance);
    private readonly CorrectionVerifier _verifier;
    private readonly CorrectionBuilder _builder;

    public CorrectionVerifierTests()
    {
        _verifier = new CorrectionVerifier(
            _reader, new OpenApiSemanticAdapter(), new ReferenceGraph());

        _builder = new CorrectionBuilder(
            new OperationCatalog(), new SplicingEmitter(new CanonicalEmitter()));
    }

    [Fact]
    public void A_correction_built_as_asked_is_established()
    {
        SpecDocument production = Read("baseline-30.yaml");
        BuiltCorrection built = Build(production);

        CorrectionVerification verification = _verifier.Verify(
            built.Serialized, production, built, Ruleset.Default);

        verification.Established.Should().BeTrue(verification.Explanation);
        verification.Explanation.Should().Contain("identique à l'octet");
    }

    [Fact]
    public void Bytes_that_do_not_parse_fail_at_the_first_gate_and_not_the_last()
    {
        SpecDocument production = Read("baseline-30.yaml");
        BuiltCorrection built = Build(production);

        CorrectionVerification verification = _verifier.Verify(
            Encoding.UTF8.GetBytes("ceci: n'est pas: une description"), production, built,
            Ruleset.Default);

        verification.Reparsed.Should().BeFalse();
        verification.Established.Should().BeFalse();
        verification.Explanation.Should().Contain("ne se relit pas");
    }

    [Fact]
    public void A_builder_that_touches_an_unselected_route_publishes_nothing()
    {
        SpecDocument production = Read("baseline-30.yaml");
        BuiltCorrection built = Build(production);

        // Le défaut injecté : une route que personne n'avait retenue perd son identifiant d'opération.
        string sabotaged = Encoding.UTF8.GetString(built.Serialized.Span)
            .Replace("operationId: listerDocuments", "operationId: saboté", StringComparison.Ordinal);

        sabotaged.Should().Contain("saboté", "le corpus doit porter l'opération sabotée");

        CorrectionVerification verification = _verifier.Verify(
            Encoding.UTF8.GetBytes(sabotaged), production, built, Ruleset.Default);

        verification.UnselectedUntouched.Should().BeFalse();
        verification.Established.Should().BeFalse();
        verification.Explanation.Should().Contain("personne n'avait retenu");
    }

    [Fact]
    public void A_correction_that_did_nothing_is_refused_too()
    {
        SpecDocument production = Read("baseline-30.yaml");
        BuiltCorrection built = Build(production);

        // La production inchangée : rien n'a dérivé, et rien n'a été fait non plus.
        CorrectionVerification verification = _verifier.Verify(
            production.Source, production, built, Ruleset.Default);

        verification.UnselectedUntouched.Should().BeTrue("rien n'a bougé");
        verification.SelectedAsChosen.Should().BeFalse("mais ce qui devait bouger n'a pas bougé");
        verification.Established.Should().BeFalse();
        verification.Explanation.Should().Contain("n'a pas changé comme demandé");
    }

    [Fact]
    public void A_document_carrying_anything_the_selection_did_not_ask_for_is_refused()
    {
        // Cet essai en remplace un qui vérifiait qu'un élément dépouillé de sa trace d'origine était
        // refusé. Les traces ne sont plus écrites, et leur retrait a resserré la vérification plutôt que
        // de l'affaiblir : la liste des emplacements autorisés à changer ne contient plus que la version
        // et ce que la sélection a nommé. Un membre ajouté à la racine — ce que le bloc
        // « x-apidelta-correction » était — est donc désormais refusé comme n'importe quelle dérive.
        SpecDocument production = Read("baseline-30.yaml");
        BuiltCorrection built = Build(production);

        string tampered = Encoding.UTF8.GetString(built.Serialized.Span).TrimEnd('\n')
            + "\nx-autre-chose: vrai\n";

        CorrectionVerification verification = _verifier.Verify(
            Encoding.UTF8.GetBytes(tampered), production, built, Ruleset.Default);

        verification.UnselectedUntouched.Should().BeFalse(
            "personne n'avait retenu ce membre : il n'a pas le droit d'être là");

        verification.Established.Should().BeFalse();
        verification.Explanation.Should().Contain("que personne n'avait retenu a changé");
    }

    [Fact]
    public void A_truncated_reference_walk_stops_the_verification_instead_of_passing_it()
    {
        // Une revue a trouvé ceci : la troncature du graphe ne valait qu'un avertissement, et cette
        // vérification n'écoute que les diagnostics bloquants. Elle concluait donc « les références se
        // résolvent » depuis une traversée qui s'était arrêtée en route — si bien qu'une référence morte
        // située au-delà du budget laissait publier le document.
        SpecDocument production = Read("baseline-30.yaml");
        BuiltCorrection built = Build(production);

        Ruleset narrow = Ruleset.Default with
        {
            Limits = LoadLimits.Default with { MaxReferences = 1 },
        };

        CorrectionVerification verification = _verifier.Verify(
            built.Serialized, production, built, narrow);

        verification.Reparsed.Should().BeTrue("le document se relit : ce n'est pas lui qui est en cause");
        verification.ReferencesResolved.Should().BeFalse(
            "ce qui n'a pas été atteint n'est ni résolu ni non résolu : c'est inconnu");

        verification.Established.Should().BeFalse();
        verification.Explanation.Should().Contain("budget");
    }

    [Fact]
    public void A_complete_reference_walk_still_passes()
    {
        // L'autre moitié : un refus qui se déclenche toujours ne vaut pas mieux qu'un refus qui ne se
        // déclenche jamais.
        SpecDocument production = Read("baseline-30.yaml");
        BuiltCorrection built = Build(production);

        CorrectionVerification verification = _verifier.Verify(
            built.Serialized, production, built, Ruleset.Default);

        verification.ReferencesResolved.Should().BeTrue(verification.Explanation);
        verification.Established.Should().BeTrue(verification.Explanation);
    }

    private BuiltCorrection Build(SpecDocument production)
    {
        SpecDocument candidate = Read("candidate-additive.yaml", DocumentRole.Candidate);
        OperationSelector route = new("/dossiers/{id}", HttpOperationMethod.Get);

        // La fermeture, comme l'étape la calculera : une route reportée amène les composants qu'elle
        // référence, sans quoi la production corrigée pointerait vers un schéma qui n'existe pas. C'est
        // précisément ce que la vérification a refusé la première fois que cet essai a tourné.
        Result<DependencyClosure> closure = new DependencyResolver(new OpenApiMetamodel()).Close(
            [SpecPointer.Root.Child("paths").Child(route.Path).Child(route.MethodKey)],
            candidate,
            production,
            CancellationToken.None);

        closure.IsSuccess.Should().BeTrue(closure.Failure?.Message);

        Result<BuiltCorrection> built = _builder.Build(new CorrectionRequest(
            production,
            candidate,
            [],
            new CorrectionSelection([route]),
            closure.Value,
            Version: null,
            CanonicalFormatting.Default));

        built.IsSuccess.Should().BeTrue(built.Failure?.Message);

        return built.Value;
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
}
