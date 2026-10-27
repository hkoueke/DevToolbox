using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Dependencies;

/// <summary>
/// Rassemble ce dont un ensemble d'objets a besoin pour tenir debout seul.
/// </summary>
/// <remarks>
/// La traversée est itérative, avec une pile explicite. Non par goût : une description peut imbriquer des
/// schémas aussi profondément qu'elle le veut, et une récursion y laisserait sa pile d'appels — ce qui
/// transformerait un document légal en panne de l'outil.
/// <para>
/// L'ensemble des cibles déjà visitées est indexé par identité canonique de référence, et c'est lui qui
/// fait terminer la traversée d'un graphe cyclique. Un cycle entre schémas est légal : il est consigné,
/// non signalé.
/// </para>
/// </remarks>
public sealed class DependencyResolver : IDependencyResolver
{
    private const string ReferenceField = "$ref";

    private readonly IMetamodel _metamodel;

    /// <summary>Crée le résolveur.</summary>
    /// <param name="metamodel">Sert à comparer les homonymes sous leur forme canonique.</param>
    public DependencyResolver(IMetamodel metamodel)
    {
        ArgumentNullException.ThrowIfNull(metamodel);
        _metamodel = metamodel;
    }

    /// <inheritdoc />
    public Result<DependencyClosure> Close(
        IReadOnlyList<SpecPointer> roots,
        SpecDocument candidate,
        SpecDocument production,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(production);

        HashSet<ReferenceTarget> visited = [];
        List<ReferenceTarget> cycles = [];
        List<ReferenceTarget> external = [];
        SortedSet<string> required = new(StringComparer.Ordinal);

        Stack<SpecNode> pending = new();

        // La fermeture part des objets que le correctif emporte, non des emplacements de différence. Un
        // champ modifié ne porte aucune référence ; l'opération qui le contient en porte, et c'est elle
        // qui sera écrite. Partir du champ laisserait sortir un document aux références pendantes.
        foreach (string carried in roots
            .Select(CarriedObject.Of)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal))
        {
            if (carried.StartsWith("/components/", StringComparison.Ordinal))
            {
                required.Add(carried);
            }

            Push(candidate, carried, pending);
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Collect(pending.Pop(), candidate, visited, cycles, external, required, pending);
        }

        IReadOnlyList<SpecPointer> components =
        [
            .. required.Select(pointer => Rebuild(pointer)),
        ];

        return Result.Success(new DependencyClosure(
            components,
            external,
            cycles,
            Conflicts(components, candidate, production)));
    }

    /// <summary>
    /// Empile ce dont le contenu voyage avec un objet emporté.
    /// </summary>
    /// <remarks>
    /// Une opération ne voyage jamais seule : les champs du niveau de son chemin — « parameters »,
    /// « servers » — partent avec elle, parce qu'ils la gouvernent. Ils portent des références, et
    /// l'omission de ce détail est précisément ce qui fait sortir un document dont un paramètre partagé
    /// pend dans le vide.
    /// </remarks>
    private static void Push(SpecDocument candidate, string carried, Stack<SpecNode> pending)
    {
        if (PointerNavigator.Find(candidate.Root, carried) is { } node)
        {
            pending.Push(node);
        }

        if (!carried.StartsWith("/paths/", StringComparison.Ordinal)
            || CarriedObject.IsWholePathItem(carried))
        {
            return;
        }

        string item = carried[..carried.LastIndexOf('/')];

        if (PointerNavigator.Find(candidate.Root, item) is not MappingNode parent)
        {
            return;
        }

        foreach (MappingEntry entry in parent.Entries
            .Where(entry => !PathRows.Methods.Contains(entry.Key.Text)))
        {
            pending.Push(entry.Value);
        }
    }

    /// <summary>
    /// Parcourt un nœud, emporte ce qu'il référence, et empile ce qui reste à visiter.
    /// </summary>
    private static void Collect(
        SpecNode node,
        SpecDocument candidate,
        HashSet<ReferenceTarget> visited,
        List<ReferenceTarget> cycles,
        List<ReferenceTarget> external,
        SortedSet<string> required,
        Stack<SpecNode> pending)
    {
        switch (node)
        {
            case MappingNode mapping when mapping.Find(ReferenceField) is ScalarNode reference:
                Follow(reference, candidate, visited, cycles, external, required, pending);
                break;

            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    pending.Push(entry.Value);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    pending.Push(item);
                }

                break;
        }
    }

    private static void Follow(
        ScalarNode reference,
        SpecDocument candidate,
        HashSet<ReferenceTarget> visited,
        List<ReferenceTarget> cycles,
        List<ReferenceTarget> external,
        SortedSet<string> required,
        Stack<SpecNode> pending)
    {
        ReferenceTarget target = ReferenceTarget.Parse(reference.Text);

        if (target.IsExternal)
        {
            // Préservée telle quelle : la suivre serait sortir du document, ce que la politique décide
            // ailleurs. Ce qui compte ici est de la consigner pour que le rapport la nomme.
            if (!external.Contains(target))
            {
                external.Add(target);
            }

            return;
        }

        if (!visited.Add(target))
        {
            if (!cycles.Contains(target))
            {
                cycles.Add(target);
            }

            return;
        }

        if (PointerNavigator.Find(candidate.Root, target.Fragment) is not { } resolved)
        {
            // Une référence morte ne parvient pas jusqu'ici : la validation a refusé le document avant.
            return;
        }

        if (target.Fragment.StartsWith("/components/", StringComparison.Ordinal))
        {
            required.Add(target.Fragment);
        }

        pending.Push(resolved);
    }

    /// <summary>
    /// Reconnaît les composants homonymes dont les deux versions ne disent pas la même chose.
    /// </summary>
    /// <remarks>
    /// Comparés sous leur forme canonique : deux écritures du même composant ne sont pas un désaccord.
    /// Ce qui est rapporté, ce sont les champs qui diffèrent — « Dossier a changé » n'aide personne,
    /// « Dossier n'exige plus libelle mais exige code » se tranche.
    /// </remarks>
    private List<ComponentConflict> Conflicts(
        IReadOnlyList<SpecPointer> components,
        SpecDocument candidate,
        SpecDocument production)
    {
        List<ComponentConflict> conflicts = [];

        foreach (SpecPointer pointer in components)
        {
            if (PointerNavigator.Find(candidate.Root, pointer.Json) is not { } fromCandidate
                || PointerNavigator.Find(production.Root, pointer.Json) is not { } fromProduction)
            {
                continue;
            }

            string candidateForm = CanonicalForm.Of(
                fromCandidate, _metamodel, candidate.DeclaredVersion);

            string productionForm = CanonicalForm.Of(
                fromProduction, _metamodel, production.DeclaredVersion);

            if (string.Equals(candidateForm, productionForm, StringComparison.Ordinal))
            {
                continue;
            }

            string[] segments = pointer.Json.Split('/', StringSplitOptions.RemoveEmptyEntries);

            conflicts.Add(new ComponentConflict(
                segments[^1],
                segments.Length > 1 ? segments[^2] : "components",
                pointer,
                pointer,
                DifferingFields(fromCandidate, fromProduction, candidate, production)));
        }

        return conflicts;
    }

    private IReadOnlyList<string> DifferingFields(
        SpecNode fromCandidate,
        SpecNode fromProduction,
        SpecDocument candidate,
        SpecDocument production)
    {
        if (fromCandidate is not MappingNode left || fromProduction is not MappingNode right)
        {
            return ["(la forme même du composant diffère)"];
        }

        SortedSet<string> fields = new(StringComparer.Ordinal);

        foreach (string key in left.Entries.Select(entry => entry.Key.Text)
            .Union(right.Entries.Select(entry => entry.Key.Text), StringComparer.Ordinal))
        {
            SpecNode? here = left.Find(key);
            SpecNode? there = right.Find(key);

            if (here is null || there is null)
            {
                fields.Add(key);
                continue;
            }

            if (!string.Equals(
                CanonicalForm.Of(here, _metamodel, candidate.DeclaredVersion),
                CanonicalForm.Of(there, _metamodel, production.DeclaredVersion),
                StringComparison.Ordinal))
            {
                fields.Add(key);
            }
        }

        return [.. fields];
    }

    /// <summary>Reconstruit un pointeur à partir de sa forme textuelle.</summary>
    private static SpecPointer Rebuild(string fragment)
    {
        SpecPointer pointer = SpecPointer.Root;

        foreach (string segment in fragment.TrimStart('/').Split('/'))
        {
            pointer = pointer.Child(
                segment.Replace("~1", "/", StringComparison.Ordinal)
                    .Replace("~0", "~", StringComparison.Ordinal));
        }

        return pointer;
    }
}
