using System.Text;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>
/// Établit que la production corrigée dit ce qu'on lui a demandé, et rien de plus.
/// </summary>
/// <remarks>
/// Les deux épreuves qui comptent vont par paire. Établir que rien d'autre n'a bougé, sans établir que ce
/// qui devait bouger a bougé, laisserait passer une correction qui n'aurait rien fait ; l'inverse
/// laisserait passer une correction qui aurait fait autre chose en plus. Prises ensemble, elles disent que
/// le diff qu'un développeur lira est exactement la sélection qu'il a faite — et c'est la seule promesse
/// qui rend une production corrigée relisable.
/// </remarks>
public sealed class CorrectionVerifier : ICorrectionVerifier
{
    private const string Name = "production corrigée";
    private const string VersionPointer = "/info/version";

    private readonly ISpecReader _reader;
    private readonly ISpecValidator _validator;
    private readonly IReferenceGraph _references;

    /// <summary>Crée le vérificateur.</summary>
    /// <param name="reader">Relit les octets émis.</param>
    /// <param name="validator">Revalide la structure.</param>
    /// <param name="references">Résout les références.</param>
    public CorrectionVerifier(
        ISpecReader reader,
        ISpecValidator validator,
        IReferenceGraph references)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(references);

        _reader = reader;
        _validator = validator;
        _references = references;
    }

    /// <inheritdoc />
    public CorrectionVerification Verify(
        ReadOnlyMemory<byte> serialized,
        SpecDocument production,
        BuiltCorrection built,
        Ruleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(built);
        ArgumentNullException.ThrowIfNull(ruleset);

        SpecReadResult read = _reader.ReadBytes(serialized, Name, production.Role, ruleset.Limits);

        if (read.Document is not { } corrected)
        {
            return new CorrectionVerification(
                false, false, false, false, false, false, Said(read.Diagnostics));
        }

        IReadOnlyList<Diagnostic> verdicts = _validator.Validate(corrected);

        if (verdicts.Any(diagnostic => diagnostic.IsBlocking))
        {
            return new CorrectionVerification(
                true, false, false, false, false, false, Said(verdicts));
        }

        List<Diagnostic> resolution = [];
        ReferenceGraphResult graph = _references.Resolve(corrected, ruleset.References, resolution);

        return resolution.Any(diagnostic => diagnostic.IsBlocking) || graph.Unresolved.Any()
            ? new CorrectionVerification(true, true, false, false, false, false, Said(resolution))
            : Compare(corrected, production, built);
    }

    /// <summary>Confronte la production corrigée à la production, point par point.</summary>
    private static CorrectionVerification Compare(
        SpecDocument corrected,
        SpecDocument production,
        BuiltCorrection built)
    {
        HashSet<string> expected = Expected(built);

        List<string> drifted = Drifted(corrected, production, expected);

        if (drifted.Count > 0)
        {
            return new CorrectionVerification(true, true, true, false, false, false, drifted);
        }

        List<string> missing = Missing(corrected, production, built);

        if (missing.Count > 0)
        {
            return new CorrectionVerification(true, true, true, true, false, false, missing);
        }

        List<string> unmarked = Unmarked(corrected, built);

        return new CorrectionVerification(
            true, true, true, true, true, unmarked.Count == 0, unmarked);
    }

    /// <summary>Les emplacements que la sélection autorisait à changer.</summary>
    /// <remarks>
    /// La version et la déclaration du document en font partie sans avoir été retenues : la première est
    /// exigée par la recommandation, la seconde est ce qui empêche de prendre une proposition pour un
    /// contrat. Les nommer ici est ce qui permet au reste d'être tenu pour intouchable.
    /// </remarks>
    private static HashSet<string> Expected(BuiltCorrection built)
    {
        HashSet<string> expected = new(StringComparer.Ordinal)
        {
            VersionPointer,
            "/" + CorrectionBuilder.Declaration,
        };

        foreach (CorrectionMark mark in built.Marks)
        {
            _ = expected.Add(mark.Target.Json);
        }

        return expected;
    }

    /// <summary>
    /// Ce que la production disait et que la correction a changé sans qu'on le lui demande.
    /// </summary>
    /// <remarks>
    /// Scalaire par scalaire, et en comparant les octets : les scalaires portent tout le contenu d'une
    /// description, si bien qu'aucun d'eux n'ayant bougé, rien n'a bougé. Comparer les valeurs relues au
    /// lieu des octets laisserait passer une requotation ou une réindentation — qui ne changent pas le
    /// sens, et qui changent le diff que le relecteur doit lire.
    /// </remarks>
    private static List<string> Drifted(
        SpecDocument corrected,
        SpecDocument production,
        HashSet<string> expected)
    {
        List<string> drifted = [];

        Walk(production.Root, node =>
        {
            if (node is not ScalarNode || drifted.Count >= 10 || Covered(node.Location.Json, expected))
            {
                return;
            }

            SpecNode? here = PointerNavigator.Find(corrected.Root, node.Location.Json);

            if (here is null)
            {
                drifted.Add(node.Location.Display + " a disparu de la production corrigée");

                return;
            }

            if (!Same(production, node, corrected, here))
            {
                drifted.Add(
                    node.Location.Display + " a changé alors que personne ne l'avait retenu");
            }
        });

        return drifted;
    }

    /// <summary>Ce que la sélection avait retenu et que la correction n'a pas changé.</summary>
    private static List<string> Missing(
        SpecDocument corrected,
        SpecDocument production,
        BuiltCorrection built)
    {
        List<string> missing = [];

        foreach (SpecPointer target in built.Marks.Select(mark => mark.Target))
        {
            SpecNode? here = PointerNavigator.Find(corrected.Root, target.Json);

            if (here is null)
            {
                missing.Add(target.Display + " n'est pas dans la production corrigée");

                continue;
            }

            SpecNode? before = PointerNavigator.Find(production.Root, target.Json);

            if (before is not null && Same(production, before, corrected, here))
            {
                missing.Add(target.Display + " avait été retenu et n'a pas changé");
            }
        }

        return missing;
    }

    /// <summary>Les éléments neufs qui ne disent pas d'où ils viennent.</summary>
    private static List<string> Unmarked(SpecDocument corrected, BuiltCorrection built)
    {
        List<string> unmarked = [];

        IEnumerable<SpecPointer> additions = built.Marks
            .Where(mark => mark.IsAddition)
            .Select(mark => mark.Target)
            .Where(target => PointerNavigator.Find(corrected.Root, target.Json) is MappingNode)
            .Where(target =>
                PointerNavigator.Find(corrected.Root, target.Child(CorrectionMark.Member).Json) is null);

        foreach (SpecPointer target in additions)
        {
            unmarked.Add(target.Display + " ne porte pas sa trace d'origine");
        }

        return unmarked;
    }

    /// <summary>Indique qu'un emplacement est, ou est sous, un emplacement autorisé à changer.</summary>
    private static bool Covered(string pointer, HashSet<string> expected)
    {
        if (expected.Contains(pointer))
        {
            return true;
        }

        return expected.Any(
            allowed => pointer.StartsWith(allowed + "/", StringComparison.Ordinal));
    }

    /// <summary>Indique que deux nœuds occupent les mêmes octets, dans leurs documents respectifs.</summary>
    private static bool Same(
        SpecDocument left,
        SpecNode here,
        SpecDocument right,
        SpecNode there)
    {
        ReadOnlySpan<byte> first = Bytes(left, here);
        ReadOnlySpan<byte> second = Bytes(right, there);

        return first.SequenceEqual(second);
    }

    private static ReadOnlySpan<byte> Bytes(SpecDocument document, SpecNode node)
    {
        ReadOnlySpan<byte> source = document.Source.Span;
        int start = Math.Min((int)node.Span.Start, source.Length);
        int end = Math.Min((int)node.Span.End, source.Length);

        return end <= start ? [] : source[start..end];
    }

    /// <summary>
    /// Parcourt les valeurs de l'arbre, et non ses clés.
    /// </summary>
    /// <remarks>
    /// Une clé et sa valeur portent le <em>même</em> emplacement — le constructeur d'arbre donne à la clé
    /// le pointeur de l'entrée. Chercher une clé par son emplacement rend donc la valeur, et comparer les
    /// deux signalerait une dérive à chaque entrée du document. Les clés sont néanmoins couvertes : une clé
    /// qui aurait changé fait échouer la recherche de sa valeur, ce qui est relevé comme une disparition.
    /// </remarks>
    private static void Walk(SpecNode node, Action<SpecNode> visitor)
    {
        visitor(node);

        switch (node)
        {
            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    Walk(entry.Value, visitor);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    Walk(item, visitor);
                }

                break;

            default:
                break;
        }
    }

    private static IReadOnlyList<string> Said(IEnumerable<Diagnostic> diagnostics) =>
        [.. diagnostics.Where(diagnostic => diagnostic.IsBlocking).Select(diagnostic => diagnostic.Message)];
}
