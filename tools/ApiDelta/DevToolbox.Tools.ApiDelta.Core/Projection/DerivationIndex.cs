using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>
/// Retrouve, derrière un endroit du document projeté, l'endroit du contrat qui l'a produit.
/// </summary>
/// <remarks>
/// Le constructeur de correction travaille sur un document projeté comme sur n'importe quel candidat :
/// il y lit des routes et des composants, et n'a aucun moyen de savoir d'où ils viennent. Cet index est
/// ce moyen, et il est <em>dérivé</em> du contrat et de la projection plutôt que produit par elles — la
/// projection reste ainsi inchangée, et un index qui se tromperait ne pourrait pas corrompre le document
/// qu'elle a produit.
/// <para>
/// Les écarts sont rangés par sujet, et non par emplacement. Une règle de projection nomme l'opération ou
/// le type qu'elle regardait, jamais le mot-clé JSON qu'elle n'a pas écrit : c'est par ce nom que le
/// constructeur retrouve ce qui manque à ce qu'il porte.
/// </para>
/// </remarks>
public sealed class DerivationIndex
{
    private const string Paths = "paths";
    private const string Components = "components";
    private const string Reference = "$ref";
    private const string ComponentPrefix = "#/components/schemas/";

    private readonly Dictionary<string, DerivedOrigin> _routes;
    private readonly Dictionary<string, DerivedOrigin> _components;
    private readonly Dictionary<string, List<ProjectionGap>> _operationGaps;
    private readonly Dictionary<string, List<ProjectionGap>> _typeGaps;

    private DerivationIndex(
        Dictionary<string, DerivedOrigin> routes,
        Dictionary<string, DerivedOrigin> components,
        Dictionary<string, List<ProjectionGap>> operationGaps,
        Dictionary<string, List<ProjectionGap>> typeGaps,
        IReadOnlyDictionary<string, DerivedRoute> derivations)
    {
        _routes = routes;
        _components = components;
        _operationGaps = operationGaps;
        _typeGaps = typeGaps;
        Derivations = derivations;
    }

    /// <summary>Un index vide : le candidat n'est pas un contrat de service.</summary>
    public static DerivationIndex Empty { get; } = new(
        [], [], [], [], new Dictionary<string, DerivedRoute>(StringComparer.Ordinal));

    /// <summary>L'itinéraire dérivé de chaque opération, et comment il l'a été.</summary>
    /// <remarks>
    /// Porté jusqu'au rapport : la dérivation doit s'y lire opération par opération, parce qu'un
    /// développeur qui voit « POST /documents » sans savoir d'où le chemin vient ne peut que l'accepter
    /// ou le refuser, et non le corriger.
    /// </remarks>
    public IReadOnlyDictionary<string, DerivedRoute> Derivations { get; }

    /// <summary>Indique que l'index ne sait rien : il n'y a pas eu de projection.</summary>
    public bool IsEmpty => _routes.Count == 0 && _components.Count == 0;

    /// <summary>Dresse l'index depuis le contrat lu et ce que la projection en a fait.</summary>
    /// <param name="contract">Le contrat, qui porte les emplacements.</param>
    /// <param name="projection">La projection, qui porte les itinéraires et les écarts.</param>
    /// <returns>L'index.</returns>
    public static DerivationIndex From(WsdlContract contract, ProjectionResult projection)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(projection);

        Dictionary<string, List<ProjectionGap>> operations = Grouped(
            projection.Gaps,
            new HashSet<string>(
                contract.Operations.Select(operation => operation.Name), StringComparer.Ordinal));

        Dictionary<string, List<ProjectionGap>> types = Grouped(
            projection.Gaps,
            new HashSet<string>(contract.Types.Keys.Select(Local), StringComparer.Ordinal));

        return new DerivationIndex(
            Routes(contract, projection, operations),
            SchemaOrigins(contract, types),
            operations,
            types,
            projection.Routes);
    }

    /// <summary>D'où une route du document projeté vient.</summary>
    /// <param name="method">Sa méthode.</param>
    /// <param name="path">Son chemin.</param>
    /// <returns>L'origine, ou rien si la route n'a pas été projetée.</returns>
    public DerivedOrigin? ForRoute(string method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        return _routes.GetValueOrDefault(Key(method, path));
    }

    /// <summary>D'où un composant du document projeté vient.</summary>
    /// <param name="name">Son nom sous « components/schemas ».</param>
    /// <returns>L'origine, ou rien si le composant n'a pas été projeté.</returns>
    public DerivedOrigin? ForComponent(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _components.GetValueOrDefault(Local(name));
    }

    /// <summary>D'où un emplacement quelconque du document projeté vient.</summary>
    /// <param name="location">L'emplacement.</param>
    /// <returns>L'origine, ou rien.</returns>
    /// <remarks>
    /// Les deux formes que le constructeur de correction porte — une route entière, un composant — et
    /// rien d'autre. Un emplacement plus profond qu'une opération relève de l'opération qui le contient,
    /// et c'est là que sa trace est posée : OpenAPI n'autorise les membres d'extension que sur des
    /// objets, et la trace du plus proche objet porteur est celle que le lecteur trouvera.
    /// </remarks>
    public DerivedOrigin? For(SpecPointer location)
    {
        ArgumentNullException.ThrowIfNull(location);

        IReadOnlyList<string> segments = location.Segments;

        if (segments.Count < 3)
        {
            return null;
        }

        if (string.Equals(segments[0], Paths, StringComparison.Ordinal))
        {
            return ForRoute(segments[2], segments[1]);
        }

        return string.Equals(segments[0], Components, StringComparison.Ordinal)
            ? ForComponent(segments[2])
            : null;
    }

    /// <summary>Ce que la projection n'a pas porté d'une opération, et des schémas qu'elle atteint.</summary>
    /// <param name="method">La méthode de la route.</param>
    /// <param name="path">Son chemin.</param>
    /// <param name="carried">Le nœud projeté qui sera porté, d'où les renvois sont relevés.</param>
    /// <returns>Les écarts, dans l'ordre où la projection les a relevés.</returns>
    /// <remarks>
    /// Les schémas que la route atteint comptent, et c'est le point de l'exigence : une opération dont la
    /// réponse n'a pas pu être dérivée en entier doit le dire <em>sur elle-même</em>. Un marqueur posé
    /// seulement sur le composant laisserait croire, à qui lit l'opération, que sa réponse est complète.
    /// </remarks>
    public IReadOnlyList<ProjectionGap> UnfilledAtRoute(
        string method, string path, SpecNode? carried)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        if (_routes.GetValueOrDefault(Key(method, path)) is not { } origin)
        {
            return [];
        }

        List<ProjectionGap> unfilled = [.. NotCarried(_operationGaps, origin.Subject)];

        foreach (string component in References(carried))
        {
            unfilled.AddRange(NotCarried(_typeGaps, component));
        }

        return unfilled;
    }

    /// <summary>Ce que la projection n'a pas porté d'un composant.</summary>
    /// <param name="name">Son nom sous « components/schemas ».</param>
    /// <returns>Les écarts, dans l'ordre où la projection les a relevés.</returns>
    public IReadOnlyList<ProjectionGap> UnfilledAtComponent(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return [.. NotCarried(_typeGaps, name)];
    }

    /// <summary>Les routes du document projeté, par méthode et chemin.</summary>
    private static Dictionary<string, DerivedOrigin> Routes(
        WsdlContract contract,
        ProjectionResult projection,
        Dictionary<string, List<ProjectionGap>> gaps)
    {
        Dictionary<string, DerivedOrigin> routes = new(StringComparer.Ordinal);

        foreach (WsdlOperation operation in contract.Operations)
        {
            if (!projection.Routes.TryGetValue(operation.Name, out DerivedRoute? route))
            {
                continue;
            }

            routes[Key(route.Method, route.Path)] = new DerivedOrigin(
                operation.Name, operation.Location, Worth(gaps, operation.Name));
        }

        return routes;
    }

    /// <summary>Les composants du document projeté, par nom local de type.</summary>
    private static Dictionary<string, DerivedOrigin> SchemaOrigins(
        WsdlContract contract, Dictionary<string, List<ProjectionGap>> gaps)
    {
        Dictionary<string, DerivedOrigin> components = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, XsdType> type in contract.Types)
        {
            string name = Local(type.Key);

            components[name] = new DerivedOrigin(
                type.Key, type.Value.Location, Worth(gaps, name));
        }

        return components;
    }

    /// <summary>
    /// Ce que la projection d'un sujet valait.
    /// </summary>
    /// <remarks>
    /// Approchante dès qu'un écart touche le sujet, qu'il soit approchant ou non porté : l'élément écrit
    /// n'est alors plus l'équivalent de ce que le contrat disait. Ce qui n'a pas été porté du tout est
    /// nommé à part, par un marqueur de non-remplissage.
    /// </remarks>
    private static ProjectionConfidence Worth(
        Dictionary<string, List<ProjectionGap>> gaps, string subject) =>
        gaps.TryGetValue(Local(subject), out List<ProjectionGap>? own) && own.Count > 0
            ? ProjectionConfidence.Approximate
            : ProjectionConfidence.Carried;

    /// <summary>Range les écarts sous ceux des sujets qui sont connus.</summary>
    private static Dictionary<string, List<ProjectionGap>> Grouped(
        IReadOnlyList<ProjectionGap> gaps, HashSet<string> subjects)
    {
        Dictionary<string, List<ProjectionGap>> grouped = new(StringComparer.Ordinal);

        foreach (ProjectionGap gap in gaps)
        {
            string subject = Local(gap.Subject);

            if (!subjects.Contains(subject))
            {
                continue;
            }

            if (!grouped.TryGetValue(subject, out List<ProjectionGap>? own))
            {
                own = [];
                grouped[subject] = own;
            }

            own.Add(gap);
        }

        return grouped;
    }

    private static IEnumerable<ProjectionGap> NotCarried(
        Dictionary<string, List<ProjectionGap>> gaps, string subject) =>
        gaps.TryGetValue(Local(subject), out List<ProjectionGap>? own)
            ? own.Where(gap => gap.Confidence == ProjectionConfidence.NotCarried)
            : [];

    /// <summary>Les composants qu'un nœud porté atteint par renvoi.</summary>
    private static HashSet<string> References(SpecNode? node)
    {
        HashSet<string> referenced = new(StringComparer.Ordinal);

        if (node is null)
        {
            return referenced;
        }

        Walk(node, scalar =>
        {
            if (scalar.Text.StartsWith(ComponentPrefix, StringComparison.Ordinal))
            {
                _ = referenced.Add(scalar.Text[ComponentPrefix.Length..]);
            }
        });

        return referenced;
    }

    /// <summary>Parcourt les scalaires qui portent un renvoi.</summary>
    private static void Walk(SpecNode node, Action<ScalarNode> visitor)
    {
        switch (node)
        {
            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    if (string.Equals(entry.Key.Text, Reference, StringComparison.Ordinal)
                        && entry.Value is ScalarNode target)
                    {
                        visitor(target);
                    }

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

    private static string Key(string method, string path) =>
        method.ToUpperInvariant() + " " + path;

    private static string Local(string qualified)
    {
        int separator = qualified.LastIndexOf(':');

        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }
}
