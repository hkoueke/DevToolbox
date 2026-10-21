using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>
/// Construit une description autonome des seules opérations retenues.
/// </summary>
/// <remarks>
/// Ce que l'extrait contient est décidé ici, et la liste est courte parce que chaque entrée a une raison.
/// La version et <c>info</c> tels quels, <c>info.version</c> comprise : un extrait n'est pas une
/// nouvelle version de l'interface, et incrémenter au passage ferait d'une vue un changement. Les chemins
/// retenus, chacun ne portant que les méthodes choisies <em>et les paramètres que ces méthodes héritent
/// de lui</em> : omettre l'héritage produirait un fichier qui se relit, qui se valide, et qui décrit
/// autre chose. Les serveurs et l'exigence de sécurité en vigueur, et seuls les mécanismes que cette
/// exigence nomme. Les étiquettes employées. Les composants de la fermeture, une fois chacun.
/// <para>
/// Et rien que la sélection n'exige pas. C'est la moitié de la promesse : un extrait qui emporterait tout
/// <c>components</c> par prudence serait aussi illisible que la source.
/// </para>
/// </remarks>
public sealed class ExtractBuilder : IExtractBuilder
{
    private const string Paths = "paths";

    private const string Components = "components";

    private readonly IDependencyResolver _dependencies;

    /// <summary>Crée le constructeur.</summary>
    /// <param name="dependencies">Ferme les dépendances des opérations retenues.</param>
    public ExtractBuilder(IDependencyResolver dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _dependencies = dependencies;
    }

    /// <inheritdoc />
    public Result<BuiltExtract> Build(
        SpecDocument source,
        ExtractRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasSelection)
        {
            return Result.Fail<BuiltExtract>(
                FailureReason.InvalidInput,
                "Aucune opération n'a été retenue : il n'existe pas d'extrait vide.");
        }

        if (Missing(source, request) is { Count: > 0 } missing)
        {
            return Result.Fail<BuiltExtract>(
                FailureReason.InvalidInput,
                "Ces opérations ne sont plus dans la description : " + string.Join(", ", missing)
                + ". Relancez la sélection sur son contenu actuel.");
        }

        Result<DependencyClosure> closure = _dependencies.Close(
            [.. request.Selectors.Select(selector => Pointer(selector))],
            source,
            source,
            cancellationToken);

        return closure.IsFailure
            ? Result.Fail<BuiltExtract>(closure.Failure!)
            : Result.Success(Assemble(source, request, closure.Value));
    }

    /// <summary>Monte le document de l'extrait, section par section.</summary>
    private static BuiltExtract Assemble(
        SpecDocument source,
        ExtractRequest request,
        DependencyClosure closure)
    {
        MappingNode root = (MappingNode)source.Root;

        List<MappingEntry> entries = [];

        Carry(entries, root, "openapi");
        Carry(entries, root, "swagger");
        Carry(entries, root, "info");

        IReadOnlyList<string> tags = Used(source, request);

        Add(entries, Paths, PathsOf(source, request));
        Add(entries, "servers", root.Find("servers"));
        Add(entries, "security", root.Find("security"));
        Add(entries, "tags", Definitions(root, tags));
        Add(entries, Components, ComponentsOf(root, closure, Schemes(root)));

        entries.AddRange(ExtensionCarryOver.DocumentLevel(root));

        return new BuiltExtract(
            TreeEdits.Mapping(SpecPointer.Root, entries),
            closure,
            tags,
            [.. request.Selectors.Select(selector => Inheritance(source, selector))]);
    }

    /// <summary>Les chemins retenus, chacun réduit aux méthodes choisies plus ce qu'elles héritent.</summary>
    private static MappingNode? PathsOf(SpecDocument source, ExtractRequest request)
    {
        List<MappingEntry> paths = [];

        foreach (IGrouping<string, OperationSelector> path in request.Selectors
            .GroupBy(selector => selector.Path, StringComparer.Ordinal)
            .OrderBy(path => path.Key, StringComparer.Ordinal))
        {
            if (EffectiveOperation.Item(source, path.First()) is not { } item)
            {
                continue;
            }

            paths.Add(new MappingEntry(TreeEdits.Key(path.Key), Trimmed(item, [.. path])));
        }

        return paths.Count == 0
            ? null
            : TreeEdits.Mapping(SpecPointer.Root.Child(Paths), paths);
    }

    /// <summary>
    /// Un élément de chemin réduit : les méthodes retenues, et les champs du niveau qui les gouvernent.
    /// </summary>
    /// <remarks>
    /// Les champs du niveau sont conservés tels quels plutôt que fusionnés dans chaque opération. Un
    /// paramètre déclaré au niveau du chemin vaut pour toutes ses opérations, et comme l'extrait ne
    /// garde que les opérations retenues, le laisser là où il était dit la même chose en restant lisible
    /// — et c'est cette équivalence que la vérification établit, forme effective contre forme effective.
    /// </remarks>
    private static MappingNode Trimmed(MappingNode item, IReadOnlyList<OperationSelector> kept)
    {
        HashSet<string> methods = new(
            kept.Select(selector => selector.MethodKey), StringComparer.Ordinal);

        return TreeEdits.Mapping(
            item.Location,
            [
                .. item.Entries.Where(entry =>
                    !PathRows.Methods.Contains(entry.Key.Text)
                    || methods.Contains(entry.Key.Text)),
            ]);
    }

    /// <summary>Les composants de la fermeture, et les mécanismes de sécurité que l'exigence nomme.</summary>
    private static MappingNode? ComponentsOf(
        MappingNode root,
        DependencyClosure closure,
        IReadOnlyList<string> schemes)
    {
        Dictionary<string, List<MappingEntry>> families = new(StringComparer.Ordinal);

        foreach (SpecPointer required in closure.Required)
        {
            if (required.Segments.Count != 3
                || PointerNavigator.Find(root, required.Json) is not { } node)
            {
                continue;
            }

            Family(families, required.Segments[1]).Add(
                new MappingEntry(TreeEdits.Key(required.Segments[2]), node));
        }

        foreach (string scheme in schemes)
        {
            if (PointerNavigator.Find(root, "/components/securitySchemes/" + scheme) is { } node)
            {
                Family(families, "securitySchemes").Add(
                    new MappingEntry(TreeEdits.Key(scheme), node));
            }
        }

        return families.Count == 0 ? null : TreeEdits.Mapping(
            SpecPointer.Root.Child(Components),
            [
                .. families
                    .OrderBy(family => family.Key, StringComparer.Ordinal)
                    .Select(family => new MappingEntry(
                        TreeEdits.Key(family.Key),
                        TreeEdits.Mapping(
                            SpecPointer.Root.Child(Components).Child(family.Key),
                            Distinct(family.Value)))),
            ]);
    }

    /// <summary>Les mécanismes que l'exigence de sécurité du document nomme.</summary>
    private static IReadOnlyList<string> Schemes(MappingNode root) =>
    [
        .. (root.Find("security") as SequenceNode)?.Items
            .OfType<MappingNode>()
            .SelectMany(requirement => requirement.Entries)
            .Select(entry => entry.Key.Text)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList() ?? [],
    ];

    /// <summary>Les étiquettes que les opérations retenues emploient.</summary>
    private static IReadOnlyList<string> Used(SpecDocument source, ExtractRequest request) =>
    [
        .. request.Selectors
            .Select(selector => EffectiveOperation.Item(source, selector)?.Find(selector.MethodKey))
            .OfType<MappingNode>()
            .Select(operation => operation.Find("tags") as SequenceNode)
            .OfType<SequenceNode>()
            .SelectMany(tags => tags.Items.OfType<ScalarNode>())
            .Select(tag => tag.Text)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal),
    ];

    /// <summary>Les définitions d'étiquettes employées, et seulement elles.</summary>
    private static SequenceNode? Definitions(MappingNode root, IReadOnlyList<string> used)
    {
        if (root.Find("tags") is not SequenceNode tags || used.Count == 0)
        {
            return null;
        }

        IReadOnlyList<SpecNode> kept =
        [
            .. tags.Items
                .OfType<MappingNode>()
                .Where(tag => tag.Find("name") is ScalarNode name && used.Contains(name.Text, StringComparer.Ordinal)),
        ];

        return kept.Count == 0 ? null : TreeEdits.Sequence(SpecPointer.Root.Child("tags"), kept);
    }

    /// <summary>Ce que l'opération hérite, consigné pour être affiché.</summary>
    private static InheritanceRecord Inheritance(SpecDocument source, OperationSelector selector)
    {
        MappingNode? effective = EffectiveOperation.Of(source, selector);

        return new InheritanceRecord(
            selector,
            EffectiveOperation.InheritedParameters(source, selector),
            Addresses(effective?.Find("servers") as SequenceNode),
            Named(effective?.Find("security") as SequenceNode));
    }

    private static IReadOnlyList<string> Addresses(SequenceNode? servers) =>
    [
        .. (servers?.Items ?? [])
            .OfType<MappingNode>()
            .Select(server => (server.Find("url") as ScalarNode)?.Text)
            .OfType<string>(),
    ];

    private static IReadOnlyList<string> Named(SequenceNode? security) =>
    [
        .. (security?.Items ?? [])
            .OfType<MappingNode>()
            .SelectMany(requirement => requirement.Entries)
            .Select(entry => entry.Key.Text)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>Les opérations retenues que la description ne porte plus.</summary>
    /// <remarks>
    /// Nommées, et non comptées. Une reprise dont une opération a disparu doit dire laquelle : c'est la
    /// différence entre « relancez la sélection » et « relancez la sélection, GET /dossiers n'existe
    /// plus ».
    /// </remarks>
    private static IReadOnlyList<string> Missing(SpecDocument source, ExtractRequest request) =>
    [
        .. request.Selectors
            .Where(selector => EffectiveOperation.Of(source, selector) is null)
            .Select(selector => selector.Display),
    ];

    private static SpecPointer Pointer(OperationSelector selector) =>
        SpecPointer.Root.Child(Paths).Child(selector.Path).Child(selector.MethodKey);

    private static List<MappingEntry> Family(
        Dictionary<string, List<MappingEntry>> families,
        string name)
    {
        if (!families.TryGetValue(name, out List<MappingEntry>? entries))
        {
            entries = [];
            families[name] = entries;
        }

        return entries;
    }

    /// <summary>Chaque composant une fois, même si deux chemins l'ont amené.</summary>
    private static IReadOnlyList<MappingEntry> Distinct(IReadOnlyList<MappingEntry> entries) =>
    [
        .. entries
            .GroupBy(entry => entry.Key.Text, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.First()),
    ];

    private static void Carry(List<MappingEntry> entries, MappingNode root, string key) =>
        Add(entries, key, root.Find(key));

    private static void Add(List<MappingEntry> entries, string key, SpecNode? value)
    {
        if (value is not null)
        {
            entries.Add(new MappingEntry(TreeEdits.Key(key), value));
        }
    }
}
