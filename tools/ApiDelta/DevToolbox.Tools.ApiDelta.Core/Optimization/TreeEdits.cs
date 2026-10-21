using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// Les trois éditions dont l'optimiseur a besoin, chacune rendant un arbre neuf.
/// </summary>
/// <remarks>
/// Neuf, et non modifié sur place, et c'est ce qui rend l'optimiseur vérifiable : l'arbre d'origine reste
/// disponible intact pour la comparaison d'équivalence, à côté de celui qu'on propose. Une édition en
/// place aurait détruit le témoin au moment précis où l'on en a besoin.
/// <para>
/// Les nœuds reconstruits reçoivent leur véritable emplacement. Un sous-arbre déplacé qui garderait
/// l'emplacement de sa provenance mentirait sur où il est, et le métamodèle — qui lit l'emplacement pour
/// savoir ce qu'un champ est — s'y tromperait.
/// </para>
/// </remarks>
public static class TreeEdits
{
    /// <summary>Remplace le nœud d'un emplacement.</summary>
    /// <param name="root">La racine.</param>
    /// <param name="target">L'emplacement à remplacer.</param>
    /// <param name="replacement">Ce qu'on met à la place.</param>
    /// <returns>Une racine neuve, ou l'ancienne si l'emplacement ne désigne rien.</returns>
    public static SpecNode Replace(SpecNode root, SpecPointer target, SpecNode replacement)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(replacement);

        return Edit(root, SpecPointer.Root, target.Segments, 0, (_, pointer) => Repoint(replacement, pointer));
    }

    /// <summary>Pose une valeur sous une clé, en créant les associations manquantes du chemin.</summary>
    /// <param name="root">La racine.</param>
    /// <param name="target">L'emplacement à créer ou remplacer.</param>
    /// <param name="value">La valeur à y poser.</param>
    /// <returns>Une racine neuve.</returns>
    /// <remarks>
    /// Créer les associations manquantes est nécessaire : un document sans <c>components</c> doit pouvoir
    /// en recevoir un, et un document qui en a un mais sans <c>parameters</c> doit pouvoir recevoir cette
    /// famille. L'alternative — refuser — rendrait la première extraction impossible.
    /// </remarks>
    public static SpecNode Insert(SpecNode root, SpecPointer target, SpecNode value)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(value);

        return Put(root, SpecPointer.Root, target.Segments, 0, value);
    }

    /// <summary>Retire ce qu'un emplacement désigne.</summary>
    /// <param name="root">La racine.</param>
    /// <param name="target">L'emplacement à retirer.</param>
    /// <returns>Une racine neuve, ou l'ancienne si l'emplacement ne désigne rien.</returns>
    public static SpecNode Remove(SpecNode root, SpecPointer target)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(target);

        return target.Segments.Count == 0
            ? root
            : Edit(root, SpecPointer.Root, target.Segments, 0, (_, _) => null);
    }

    /// <summary>La référence qui remplace une occurrence extraite.</summary>
    /// <param name="target">Le pointeur du composant, sous la forme <c>#/components/parameters/X</c>.</param>
    /// <returns>Le nœud de référence.</returns>
    public static MappingNode Reference(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        return Mapping(
            SpecPointer.Root,
            [
                new MappingEntry(
                    Scalar(SpecPointer.Root, "$ref"), Scalar(SpecPointer.Root, target)),
            ]);
    }

    /// <summary>Le scalaire d'un drapeau booléen.</summary>
    /// <param name="value">Sa valeur.</param>
    /// <returns>Le nœud.</returns>
    public static ScalarNode Flag(bool value) =>
        new(
            SpecPointer.Root,
            Origin,
            Nowhere,
            value ? "true" : "false",
            ScalarStyle.Plain,
            new ResolvedScalar(ScalarValueKind.Boolean, value ? "true" : "false"));

    /// <summary>La clé d'une entrée neuve.</summary>
    /// <param name="text">Son texte.</param>
    /// <returns>Le nœud.</returns>
    public static ScalarNode Key(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Scalar(SpecPointer.Root, text);
    }

    /// <summary>Une association neuve.</summary>
    /// <param name="location">Où elle se trouvera.</param>
    /// <param name="entries">Ses entrées.</param>
    /// <returns>Le nœud.</returns>
    public static MappingNode Mapping(SpecPointer location, IReadOnlyList<MappingEntry> entries) =>
        new(location, Origin, Nowhere, entries);

    /// <summary>Une suite neuve.</summary>
    /// <param name="location">Où elle se trouvera.</param>
    /// <param name="items">Ses éléments.</param>
    /// <returns>Le nœud.</returns>
    public static SequenceNode Sequence(SpecPointer location, IReadOnlyList<SpecNode> items) =>
        new(location, Origin, Nowhere, items);

    /// <summary>La position d'un nœud qui n'a jamais été écrit.</summary>
    private static SourcePosition Origin => new(1, 1);

    /// <summary>L'étendue d'un nœud qui n'occupe aucun octet du fichier d'origine.</summary>
    private static ByteSpan Nowhere => new(0, 0);

    private static ScalarNode Scalar(SpecPointer location, string text) =>
        new(location, Origin, Nowhere, text, ScalarStyle.Plain, ResolvedScalar.FromString(text));

    /// <summary>
    /// Descend jusqu'à l'emplacement visé, puis reconstruit le chemin en appliquant l'édition.
    /// </summary>
    /// <remarks>
    /// L'édition rend <see langword="null"/> pour retirer. Les sous-arbres non traversés sont conservés
    /// tels quels : ils n'ont pas changé d'emplacement, et leur recopier un emplacement identique
    /// coûterait un parcours complet de l'arbre à chaque édition.
    /// </remarks>
    private static SpecNode Edit(
        SpecNode node,
        SpecPointer pointer,
        IReadOnlyList<string> segments,
        int depth,
        Func<SpecNode, SpecPointer, SpecNode?> edit)
    {
        if (depth >= segments.Count)
        {
            return edit(node, pointer) ?? node;
        }

        return node switch
        {
            MappingNode mapping => EditMapping(mapping, pointer, segments, depth, edit),
            SequenceNode sequence => EditSequence(sequence, pointer, segments, depth, edit),
            _ => node,
        };
    }

    private static MappingNode EditMapping(
        MappingNode mapping,
        SpecPointer pointer,
        IReadOnlyList<string> segments,
        int depth,
        Func<SpecNode, SpecPointer, SpecNode?> edit)
    {
        string segment = segments[depth];
        List<MappingEntry> entries = [];
        bool found = false;

        foreach (MappingEntry entry in mapping.Entries)
        {
            if (!string.Equals(entry.Key.Text, segment, StringComparison.Ordinal))
            {
                entries.Add(entry);
                continue;
            }

            found = true;
            SpecPointer child = pointer.Child(segment);

            if (depth + 1 >= segments.Count)
            {
                if (edit(entry.Value, child) is { } replacement)
                {
                    entries.Add(new MappingEntry(entry.Key, replacement));
                }

                continue;
            }

            entries.Add(new MappingEntry(
                entry.Key, Edit(entry.Value, child, segments, depth + 1, edit)));
        }

        return found ? Mapping(pointer, entries) : mapping;
    }

    private static SequenceNode EditSequence(
        SequenceNode sequence,
        SpecPointer pointer,
        IReadOnlyList<string> segments,
        int depth,
        Func<SpecNode, SpecPointer, SpecNode?> edit)
    {
        if (!int.TryParse(
                segments[depth], System.Globalization.CultureInfo.InvariantCulture, out int index)
            || index < 0
            || index >= sequence.Items.Count)
        {
            return sequence;
        }

        List<SpecNode> items = [];

        for (int rank = 0; rank < sequence.Items.Count; rank++)
        {
            SpecNode item = sequence.Items[rank];

            if (rank != index)
            {
                items.Add(item);
                continue;
            }

            SpecPointer child = pointer.Item(rank);

            if (depth + 1 >= segments.Count)
            {
                if (edit(item, child) is { } replacement)
                {
                    items.Add(replacement);
                }

                continue;
            }

            items.Add(Edit(item, child, segments, depth + 1, edit));
        }

        // Retirer un élément décale les suivants : leurs emplacements sont donc recalculés.
        return items.Count == sequence.Items.Count
            ? Sequence(pointer, items)
            : Sequence(pointer, [.. items.Select((item, rank) => Repoint(item, pointer.Item(rank)))]);
    }

    /// <summary>Pose une valeur, en créant les associations manquantes du chemin.</summary>
    private static SpecNode Put(
        SpecNode node,
        SpecPointer pointer,
        IReadOnlyList<string> segments,
        int depth,
        SpecNode value)
    {
        if (depth >= segments.Count)
        {
            return Repoint(value, pointer);
        }

        string segment = segments[depth];
        MappingNode mapping = node as MappingNode ?? Mapping(pointer, []);

        List<MappingEntry> entries = [];
        bool found = false;

        foreach (MappingEntry entry in mapping.Entries)
        {
            if (string.Equals(entry.Key.Text, segment, StringComparison.Ordinal))
            {
                found = true;

                entries.Add(new MappingEntry(
                    entry.Key,
                    Put(entry.Value, pointer.Child(segment), segments, depth + 1, value)));

                continue;
            }

            entries.Add(entry);
        }

        if (!found)
        {
            entries.Add(new MappingEntry(
                Scalar(pointer, segment),
                Put(Mapping(pointer.Child(segment), []), pointer.Child(segment), segments, depth + 1, value)));
        }

        return Mapping(pointer, entries);
    }

    /// <summary>
    /// Recopie un sous-arbre en lui donnant son véritable emplacement.
    /// </summary>
    /// <remarks>
    /// Nécessaire pour un sous-arbre déplacé : l'occurrence extraite vers <c>components</c> porte encore
    /// l'emplacement de l'opération dont elle vient, et le métamodèle lit l'emplacement pour savoir ce
    /// qu'un champ est.
    /// </remarks>
    private static SpecNode Repoint(SpecNode node, SpecPointer location) => node switch
    {
        MappingNode mapping => Mapping(
            location,
            [
                .. mapping.Entries.Select(entry => new MappingEntry(
                    Scalar(location, entry.Key.Text),
                    Repoint(entry.Value, location.Child(entry.Key.Text)))),
            ]),

        SequenceNode sequence => Sequence(
            location,
            [.. sequence.Items.Select((item, rank) => Repoint(item, location.Item(rank)))]),

        ScalarNode scalar => new ScalarNode(
            location, scalar.Position, scalar.Span, scalar.Text, scalar.Style, scalar.Resolved, scalar.Tag),

        _ => node,
    };
}
