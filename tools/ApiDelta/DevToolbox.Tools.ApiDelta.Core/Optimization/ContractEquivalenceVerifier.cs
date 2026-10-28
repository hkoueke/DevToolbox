using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Relit les octets produits, les revalide, résout leurs références, et compare le contrat.
/// </summary>
/// <remarks>
/// Quatre épreuves, dans cet ordre, parce que chacune suppose la précédente : on ne valide pas ce qui ne
/// se relit pas, on ne résout pas les références d'un document invalide, et on ne compare pas un contrat
/// dont les références pendent.
/// <para>
/// La comparaison se fait sous le profil du contrat, non sous le profil exigeant. C'est voulu :
/// l'optimisation réécrit le document — indentation, quotation, ordre des clés — si bien qu'une
/// comparaison exigeante échouerait sur une remise en forme qui ne change rien pour personne. Ce qu'il
/// faut établir est que le contrat est le même, et c'est exactement ce que ce profil compare.
/// </para>
/// <para>
/// L'extraction remplace des objets par des références : une comparaison naïve verrait partout des
/// différences. Les deux documents sont donc comparés <em>références résolues</em>, ce qui est la seule
/// façon de comparer ce qu'un appelant constate plutôt que la façon dont c'est écrit.
/// </para>
/// </remarks>
public sealed class ContractEquivalenceVerifier : IContractEquivalenceVerifier
{
    private const string Name = "copie-optimisee";

    private readonly ISpecReader _reader;
    private readonly ISpecValidator _validator;
    private readonly IReferenceGraph _references;
    private readonly IFingerprinter _fingerprinter;

    /// <summary>Crée le vérificateur.</summary>
    /// <param name="reader">Relit les octets produits.</param>
    /// <param name="validator">Revalide la structure.</param>
    /// <param name="references">Résout les références.</param>
    /// <param name="fingerprinter">Empreinte, pour comparer le contrat.</param>
    public ContractEquivalenceVerifier(
        ISpecReader reader,
        ISpecValidator validator,
        IReferenceGraph references,
        IFingerprinter fingerprinter)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(fingerprinter);

        _reader = reader;
        _validator = validator;
        _references = references;
        _fingerprinter = fingerprinter;
    }

    /// <inheritdoc />
    public VerificationResult Verify(
        ReadOnlyMemory<byte> serialized,
        SpecDocument original,
        Ruleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(ruleset);

        SpecReadResult read = _reader.ReadBytes(
            serialized, Name, original.Role, ruleset.Limits);

        if (read.Document is not { } reparsed)
        {
            return new VerificationResult(
                false, false, false, false, Said(read.Diagnostics));
        }

        IReadOnlyList<Diagnostic> verdicts = _validator.Validate(reparsed);

        if (verdicts.Any(diagnostic => diagnostic.IsBlocking))
        {
            return new VerificationResult(true, false, false, false, Said(verdicts));
        }

        List<Diagnostic> resolution = [];
        ReferenceGraphResult graph = _references.Resolve(reparsed, ruleset.References, ruleset.Limits, resolution);

        IReadOnlyList<string> dangling = Dangling(reparsed);

        if (resolution.Any(diagnostic => diagnostic.IsBlocking)
            || graph.Unresolved.Any()
            || dangling.Count > 0)
        {
            return new VerificationResult(
                true, true, false, false, [.. Said(resolution), .. dangling]);
        }

        return Compare(reparsed, original);
    }

    /// <summary>Compare les deux contrats, références résolues de part et d'autre.</summary>
    private VerificationResult Compare(SpecDocument reparsed, SpecDocument original)
    {
        SpecNode ours = Flattened(original);
        SpecNode theirs = Flattened(reparsed);

        string before = _fingerprinter.Fingerprint(
            ours, EquivalenceProfile.Contract, original.DeclaredVersion);
        string after = _fingerprinter.Fingerprint(
            theirs, EquivalenceProfile.Contract, reparsed.DeclaredVersion);

        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return new VerificationResult(true, true, true, true, []);
        }

        return new VerificationResult(
            true, true, true, false, Divergence(ours, theirs, original.DeclaredVersion));
    }

    /// <summary>
    /// Le document, ses références locales remplacées par ce qu'elles désignent.
    /// </summary>
    /// <remarks>
    /// C'est ce qui rend l'extraction comparable : un objet et la référence qui le désigne disent la même
    /// chose à un appelant, et seule cette forme-là permet de le constater. La résolution est bornée en
    /// profondeur, car un document cyclique en ferait une descente infinie — un cycle est refusé à la
    /// lecture, mais le vérificateur n'a pas à en dépendre pour terminer.
    /// </remarks>
    /// <summary>
    /// Le document, ses références résolues, et son magasin de composants écarté.
    /// </summary>
    /// <remarks>
    /// Écarté, et c'est la décision qui rend cette vérification utilisable. Une fois les références
    /// résolues, tout ce qui est atteignable a été replacé à l'endroit où il sert : le magasin ne porte
    /// plus aucune information de contrat, il porte la <em>façon</em> dont le contrat est rangé. Le
    /// comparer ferait échouer l'extraction — qui ajoute un composant — et la suppression d'un composant
    /// inaccessible, c'est-à-dire exactement les deux transformations que cet outil existe pour faire.
    /// <para>
    /// Un mécanisme de sécurité échappe à ce raisonnement : une exigence le désigne par sa clé, non par
    /// une référence, si bien que la résolution ne le replace nulle part. Ce n'est pas la comparaison qui
    /// s'en charge mais le contrôle d'intégrité qui précède — ce qu'une exigence nomme doit exister, et
    /// c'est une question de références qui pendent, non de contrat qui change.
    /// </para>
    /// </remarks>
    private static SpecNode Flattened(SpecDocument document)
    {
        const string Components = "components";

        if (Flatten(document.Root, document.Root, SpecPointer.Root, depth: 0)
            is not MappingNode flattened)
        {
            return document.Root;
        }

        return TreeEdits.Mapping(
            SpecPointer.Root,
            [
                .. flattened.Entries.Where(entry =>
                    !string.Equals(entry.Key.Text, Components, StringComparison.Ordinal)),
            ]);
    }

    /// <summary>
    /// Les mécanismes de sécurité qu'une exigence nomme et que le document ne déclare pas.
    /// </summary>
    /// <remarks>
    /// Une exigence de sécurité n'est pas une référence : le graphe ne la voit pas. Sans ce contrôle, un
    /// nettoyage qui retirerait un mécanisme encore exigé produirait un document que l'outil déclarerait
    /// équivalent, et qui refuserait tout appel.
    /// </remarks>
    private static IReadOnlyList<string> Dangling(SpecDocument document)
    {
        HashSet<string> declared = new(StringComparer.Ordinal);

        if (PointerNavigator.Find(document.Root, "/components/securitySchemes") is MappingNode schemes)
        {
            foreach (MappingEntry scheme in schemes.Entries)
            {
                declared.Add(scheme.Key.Text);
            }
        }

        return
        [
            .. Required(document.Root)
                .Where(name => !declared.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => "mécanisme de sécurité exigé et non déclaré : " + name),
        ];
    }

    /// <summary>Les clés que les exigences de sécurité du document nomment, où qu'elles soient.</summary>
    private static IEnumerable<string> Required(SpecNode node)
    {
        Stack<SpecNode> pending = new();
        pending.Push(node);

        while (pending.Count > 0)
        {
            SpecNode current = pending.Pop();

            if (current is SequenceNode sequence)
            {
                foreach (SpecNode item in sequence.Items)
                {
                    pending.Push(item);
                }

                continue;
            }

            if (current is not MappingNode mapping)
            {
                continue;
            }

            foreach (MappingEntry entry in mapping.Entries)
            {
                if (string.Equals(entry.Key.Text, "security", StringComparison.Ordinal)
                    && entry.Value is SequenceNode requirements)
                {
                    foreach (MappingNode requirement in requirements.Items.OfType<MappingNode>())
                    {
                        foreach (MappingEntry named in requirement.Entries)
                        {
                            yield return named.Key.Text;
                        }
                    }

                    continue;
                }

                pending.Push(entry.Value);
            }
        }
    }

    private static SpecNode Flatten(SpecNode node, SpecNode root, SpecPointer location, int depth)
    {
        const int MaxDepth = 64;

        if (depth > MaxDepth)
        {
            return node;
        }

        if (node is MappingNode mapping)
        {
            if (Local(mapping) is { } target
                && PointerNavigator.Find(root, target) is { } resolved)
            {
                return Flatten(resolved, root, location, depth + 1);
            }

            return TreeEdits.Mapping(
                location,
                [
                    .. mapping.Entries.Select(entry => new MappingEntry(
                        entry.Key,
                        Flatten(entry.Value, root, location.Child(entry.Key.Text), depth + 1))),
                ]);
        }

        return node is SequenceNode sequence
            ? TreeEdits.Sequence(
                location,
                [
                    .. sequence.Items.Select((item, rank) =>
                        Flatten(item, root, location.Item(rank), depth + 1)),
                ])
            : node;
    }

    /// <summary>Le pointeur d'une référence locale, quand le nœud en est une.</summary>
    private static string? Local(MappingNode mapping) =>
        mapping.Entries.Count == 1
        && mapping.Find("$ref") is ScalarNode reference
        && reference.Text.StartsWith("#/", StringComparison.Ordinal)
            ? reference.Text[1..]
            : null;

    /// <summary>
    /// Où les deux contrats divergent, nommé plutôt que constaté.
    /// </summary>
    /// <remarks>
    /// Un échec d'équivalence sans explication est inutilisable : le développeur ne peut ni corriger la
    /// description ni signaler le défaut de l'outil. La comparaison redescend donc jusqu'au premier
    /// endroit où les empreintes cessent de correspondre.
    /// </remarks>
    private List<string> Divergence(SpecNode ours, SpecNode theirs, OpenApiVersion version)
    {
        if (ours is not MappingNode left || theirs is not MappingNode right)
        {
            return [Describe(ours.Location)];
        }

        List<string> found = [];

        foreach (MappingEntry entry in left.Entries)
        {
            SpecNode? other = right.Find(entry.Key.Text);

            if (other is null)
            {
                found.Add("absent de la sortie : " + entry.Key.Text);
                continue;
            }

            if (!Same(entry.Value, other, version))
            {
                found.AddRange(Divergence(entry.Value, other, version));
            }
        }

        foreach (MappingEntry entry in right.Entries.Where(entry => left.Find(entry.Key.Text) is null))
        {
            found.Add("apparu dans la sortie : " + entry.Key.Text);
        }

        return found.Count > 0 ? found : [Describe(ours.Location)];
    }

    private bool Same(SpecNode left, SpecNode right, OpenApiVersion version) =>
        string.Equals(
            _fingerprinter.Fingerprint(left, EquivalenceProfile.Contract, version),
            _fingerprinter.Fingerprint(right, EquivalenceProfile.Contract, version),
            StringComparison.Ordinal);

    private static string Describe(SpecPointer location) =>
        "le contrat diffère en " + location.Json;

    private static IReadOnlyList<string> Said(IEnumerable<Diagnostic> diagnostics) =>
        [.. diagnostics.Where(diagnostic => diagnostic.IsBlocking).Select(diagnostic => diagnostic.Message)];
}
