using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Fingerprinting;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>
/// Vérifie un extrait : il se relit, il est valide, ses références se résolvent, il ne porte rien
/// d'autre, et chaque opération retenue y dit la même chose que dans la source.
/// </summary>
/// <remarks>
/// La dernière épreuve est la raison d'être de ce type, et elle porte sur la forme <em>effective</em> des
/// opérations. Comparer les opérations écrites laisserait passer un extrait qui aurait perdu un paramètre
/// déclaré au niveau du chemin ou l'exigence de sécurité globale — c'est-à-dire la panne que ce mode doit
/// être incapable de produire. Le reste de la machinerie — relire, revalider, résoudre — est celle de
/// l'équivalence de contrat, employée telle quelle plutôt que réécrite.
/// </remarks>
public sealed class ExtractVerifier : IExtractVerifier
{
    private const string Name = "extrait";

    private readonly ISpecReader _reader;
    private readonly ISpecValidator _validator;
    private readonly IReferenceGraph _references;
    private readonly IFingerprinter _fingerprinter;

    /// <summary>Crée le vérificateur.</summary>
    /// <param name="reader">Relit les octets émis.</param>
    /// <param name="validator">Revalide la structure.</param>
    /// <param name="references">Résout les références.</param>
    /// <param name="fingerprinter">Empreinte, pour comparer les opérations.</param>
    public ExtractVerifier(
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
    public ExtractVerification Verify(
        ReadOnlyMemory<byte> serialized,
        SpecDocument source,
        IReadOnlyList<OperationSelector> selectors,
        Ruleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selectors);
        ArgumentNullException.ThrowIfNull(ruleset);

        SpecReadResult read = _reader.ReadBytes(serialized, Name, source.Role, ruleset.Limits);

        if (read.Document is not { } extract)
        {
            return new ExtractVerification(false, false, false, false, false, Said(read.Diagnostics));
        }

        IReadOnlyList<Diagnostic> verdicts = _validator.Validate(extract);

        if (verdicts.Any(diagnostic => diagnostic.IsBlocking))
        {
            return new ExtractVerification(true, false, false, false, false, Said(verdicts));
        }

        List<Diagnostic> resolution = [];
        ReferenceGraphResult graph = _references.Resolve(extract, ruleset.References, resolution);

        if (resolution.Any(diagnostic => diagnostic.IsBlocking) || graph.Unresolved.Any())
        {
            return new ExtractVerification(true, true, false, false, false, Said(resolution));
        }

        return Compare(extract, source, selectors);
    }

    /// <summary>Établit que l'extrait ne porte rien de plus, puis que ce qu'il porte dit la même chose.</summary>
    private ExtractVerification Compare(
        SpecDocument extract,
        SpecDocument source,
        IReadOnlyList<OperationSelector> selectors)
    {
        List<string> extra = Unselected(extract, selectors);

        if (extra.Count > 0)
        {
            return new ExtractVerification(true, true, true, false, false, extra);
        }

        List<string> changed = Diverging(extract, source, selectors);

        return new ExtractVerification(true, true, true, true, changed.Count == 0, changed);
    }

    /// <summary>
    /// Ce que l'extrait porte et que la sélection n'avait pas retenu.
    /// </summary>
    /// <remarks>
    /// Chemin par chemin et méthode par méthode. Un extrait qui emporterait une opération voisine « parce
    /// qu'elle était dans le même élément de chemin » serait la façon la plus naturelle de se tromper, et
    /// la plus difficile à remarquer en relisant un fichier de huit cents lignes.
    /// </remarks>
    private static List<string> Unselected(
        SpecDocument extract,
        IReadOnlyList<OperationSelector> selectors)
    {
        HashSet<string> retained = new(
            selectors.Select(selector => selector.Display), StringComparer.Ordinal);

        List<string> extra = [];

        if (extract.Root is not MappingNode root || root.Find("paths") is not MappingNode paths)
        {
            return extra;
        }

        foreach (MappingEntry path in paths.Entries)
        {
            if (path.Value is not MappingNode item)
            {
                continue;
            }

            foreach (MappingEntry method in item.Entries.Where(
                entry => PathRows.Methods.Contains(entry.Key.Text)))
            {
                string display = method.Key.Text.ToUpperInvariant() + " " + path.Key.Text;

                if (!retained.Contains(display))
                {
                    extra.Add("non retenue et pourtant présente : " + display);
                }
            }
        }

        return extra;
    }

    /// <summary>Les opérations retenues dont la forme effective a changé.</summary>
    private List<string> Diverging(
        SpecDocument extract,
        SpecDocument source,
        IReadOnlyList<OperationSelector> selectors)
    {
        List<string> changed = [];

        foreach (OperationSelector selector in selectors)
        {
            MappingNode? theirs = EffectiveOperation.Of(source, selector);
            MappingNode? ours = EffectiveOperation.Of(extract, selector);

            if (theirs is null || ours is null)
            {
                changed.Add("absente de l'extrait : " + selector.Display);
                continue;
            }

            if (Difference(ours, theirs, source.DeclaredVersion) is { } difference)
            {
                changed.Add(selector.Display + " : " + difference);
            }
        }

        return changed;
    }

    /// <summary>
    /// Ce qui diffère entre les deux formes effectives, nommé plutôt que constaté.
    /// </summary>
    /// <remarks>
    /// Nommé parce qu'un échec sans explication est inutilisable : le développeur ne peut ni corriger sa
    /// description ni signaler le défaut de l'outil. La comparaison descend donc jusqu'au champ.
    /// </remarks>
    private string? Difference(MappingNode ours, MappingNode theirs, OpenApiVersion version)
    {
        if (Same(ours, theirs, version))
        {
            return null;
        }

        List<string> fields = [];

        foreach (MappingEntry entry in theirs.Entries)
        {
            SpecNode? other = ours.Find(entry.Key.Text);

            if (other is null)
            {
                fields.Add("« " + entry.Key.Text + " » a disparu");
                continue;
            }

            if (!Same(entry.Value, other, version))
            {
                fields.Add("« " + entry.Key.Text + " » a changé");
            }
        }

        foreach (MappingEntry entry in ours.Entries.Where(
            entry => theirs.Find(entry.Key.Text) is null))
        {
            fields.Add("« " + entry.Key.Text + " » est apparu");
        }

        return fields.Count > 0 ? string.Join(", ", fields) : "le contrat diffère";
    }

    private bool Same(SpecNode ours, SpecNode theirs, OpenApiVersion version) =>
        string.Equals(
            _fingerprinter.Fingerprint(ours, EquivalenceProfile.Contract, version),
            _fingerprinter.Fingerprint(theirs, EquivalenceProfile.Contract, version),
            StringComparison.Ordinal);

    private static IReadOnlyList<string> Said(IEnumerable<Diagnostic> diagnostics) =>
    [
        .. diagnostics
            .Where(diagnostic => diagnostic.IsBlocking)
            .Select(diagnostic => diagnostic.Message),
    ];
}
