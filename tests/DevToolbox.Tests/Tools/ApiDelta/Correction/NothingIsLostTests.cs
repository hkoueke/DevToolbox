using System.Text;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
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
/// Rien de ce que la production disait ne disparaît de la production corrigée.
/// </summary>
/// <remarks>
/// La promesse centrale du mode, prise scalaire par scalaire plutôt que ligne par ligne : une épreuve
/// qui cherche chaque ligne du fichier d'origine dans le résultat ne voit pas qu'une valeur a changé de
/// place, ni qu'une clé a perdu son contenu au profit d'une autre identique ailleurs.
/// <para>
/// Elle est écrite en balayant le corpus plutôt que sur un cas choisi, parce que le défaut qu'elle guette
/// ne se montre que sur une forme d'écriture particulière — et qu'on ne sait pas d'avance laquelle.
/// </para>
/// </remarks>
public sealed class NothingIsLostTests
{
    private readonly SpecReader _reader = new(NullLogger<SpecReader>.Instance);
    private readonly CorrectionBuilder _builder = new(
        new OperationCatalog(), new SplicingEmitter(new CanonicalEmitter()));

    public static TheoryData<string, string> Pairs => new()
    {
        { "baseline-30.yaml", "candidate-additive.yaml" },
        { "baseline-30.yaml", "candidate-breaking.yaml" },
        { "baseline-30.yaml", "candidate-doc-only.yaml" },
        { "baseline-30.yaml", "candidate-conflict.yaml" },
        { "baseline-30.reformatted.yaml", "candidate-additive.yaml" },
        { "baseline-31.yaml", "candidate-additive.yaml" },
        { "redundant.yaml", "candidate-additive.yaml" },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Every_scalar_the_production_carried_is_still_there_byte_for_byte(
        string productionName, string candidateName)
    {
        SpecDocument production = Read(productionName, DocumentRole.Production);
        SpecDocument candidate = Read(candidateName, DocumentRole.Candidate);

        IReadOnlyList<OperationSelector> absent = Absent(production, candidate);

        if (absent.Count == 0)
        {
            return;
        }

        foreach (OperationSelector route in absent)
        {
            Result<BuiltCorrection> built = _builder.Build(
                Request(production, candidate, new CorrectionSelection([route])));

            built.IsSuccess.Should().BeTrue(
                productionName + " + " + route.Display + " : " + built.Failure?.Message);

            SpecDocument corrected = Reread(built.Value.Serialized);

            foreach (ScalarNode scalar in Scalars(production.Root))
            {
                SpecNode? here = PointerNavigator.Find(corrected.Root, scalar.Location.Json);

                here.Should().NotBeNull(
                    scalar.Location.Display + " a disparu de la production corrigée en ajoutant « "
                        + route.Display + " » à « " + productionName + " »");

                Bytes(corrected, here).SequenceEqual(Bytes(production, scalar)).Should().BeTrue(
                    scalar.Location.Display + " a changé alors que personne ne l'avait retenu, en "
                        + "ajoutant « " + route.Display + " » à « " + productionName + " »");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void The_verification_agrees_that_nothing_moved(string productionName, string candidateName)
    {
        // Le revers : si le défaut existe et que l'épreuve d'au-dessus le rate, celle-ci doit le prendre
        // — et si le vérificateur a un angle mort, c'est l'inverse qui arrivera. Les deux ensemble
        // disent si le défaut est dans l'écriture ou dans le contrôle.
        //
        // Une vérification qui n'aboutit pas n'est pas un échec de l'outil : l'étape refuse alors de
        // publier, et c'est ce que la promesse dit. Ce qui serait un échec, c'est qu'elle n'aboutisse
        // pas sans que le plan dise pourquoi — un refus muet.
        SpecDocument production = Read(productionName, DocumentRole.Production);
        SpecDocument candidate = Read(candidateName, DocumentRole.Candidate);
        CorrectionVerifier verifier = new(_reader, new OpenApiSemanticAdapter(), new ReferenceGraph());

        foreach (OperationSelector route in Absent(production, candidate))
        {
            CorrectionRequest request = Request(
                production, candidate, new CorrectionSelection([route]));

            Result<BuiltCorrection> built = _builder.Build(request);

            built.IsSuccess.Should().BeTrue(built.Failure?.Message);

            CorrectionVerification verification = verifier.Verify(
                built.Value.Serialized, production, built.Value, Ruleset.Default);

            if (verification.Established)
            {
                continue;
            }

            // Les six épreuves du vérificateur sont séquentielles : celles qui suivent la première qui
            // échoue n'ont pas tourné, et leur « faux » ne dit donc pas qu'elles ont trouvé quelque
            // chose. On ne peut rien en conclure ici, et c'est le plan qui doit porter la raison.
            built.Value.Plan.Declined.Should().NotBeEmpty(
                productionName + " + " + route.Display + " : la vérification n'aboutit pas et le plan "
                    + "n'en dit pas la raison — " + verification.Explanation);

            verification.Explanation.Should().NotBeEmpty();
        }
    }

    /// <summary>Les routes que le candidat porte et que la production n'a pas.</summary>
    private static IReadOnlyList<OperationSelector> Absent(
        SpecDocument production, SpecDocument candidate)
    {
        OperationCatalog catalog = new();
        HashSet<OperationSelector> held =
        [
            .. catalog.List(production).Select(operation => operation.Selector),
        ];

        return
        [
            .. catalog.List(candidate)
                .Select(operation => operation.Selector)
                .Where(selector => !held.Contains(selector)),
        ];
    }

    private static CorrectionRequest Request(
        SpecDocument production, SpecDocument candidate, CorrectionSelection selection)
    {
        Result<DependencyClosure> closure = new DependencyResolver(new OpenApiMetamodel()).Close(
            [
                .. selection.Routes.Select(route =>
                    SpecPointer.Root.Child("paths").Child(route.Path).Child(route.MethodKey)),
            ],
            candidate,
            production,
            CancellationToken.None);

        closure.IsSuccess.Should().BeTrue(closure.Failure?.Message);

        return new CorrectionRequest(
            production,
            candidate,
            [],
            selection,
            closure.Value,
            Version: null,
            CanonicalFormatting.Default);
    }

    private static IEnumerable<ScalarNode> Scalars(SpecNode node)
    {
        switch (node)
        {
            case MappingNode mapping:
                foreach (ScalarNode found in mapping.Entries.SelectMany(
                    entry => Scalars(entry.Value)))
                {
                    yield return found;
                }

                break;

            case SequenceNode sequence:
                foreach (ScalarNode found in sequence.Items.SelectMany(Scalars))
                {
                    yield return found;
                }

                break;

            case ScalarNode scalar:
                yield return scalar;

                break;

            default:
                break;
        }
    }

    private static ReadOnlySpan<byte> Bytes(SpecDocument document, SpecNode node)
    {
        ReadOnlySpan<byte> source = document.Source.Span;
        int start = Math.Min((int)node.Span.Start, source.Length);
        int end = Math.Min((int)node.Span.End, source.Length);

        return end <= start ? [] : source[start..end];
    }

    private SpecDocument Read(string fileName, DocumentRole role)
    {
        SpecReadResult result = _reader.ReadBytes(
            Encoding.UTF8.GetBytes(CorpusLoader.Read(fileName)), fileName, role, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            fileName + " doit se lire : "
            + string.Join(" / ", result.Diagnostics.Select(one => one.Message)));

        return result.Document;
    }

    private SpecDocument Reread(ReadOnlyMemory<byte> bytes)
    {
        SpecReadResult result = _reader.ReadBytes(
            bytes, "corrigé", DocumentRole.Production, LoadLimits.Default);

        result.Document.Should().NotBeNull(
            "la production corrigée doit se relire : "
            + string.Join(" / ", result.Diagnostics.Select(one => one.Message)));

        return result.Document;
    }
}
