using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Extraction;

/// <summary>
/// Emporte les extensions de fournisseur dans l'extrait, et signale celles qui ne s'y retrouvent plus.
/// </summary>
/// <remarks>
/// Emportées telles quelles, celles du niveau du document comprises. L'outil ne sait pas ce qu'une
/// passerelle lit : laisser derrière une extension qu'il ne comprend pas serait décider à sa place
/// qu'elle ne servait à rien.
/// <para>
/// Une extension emportée peut nommer un chemin ou une opération que l'extrait ne contient pas — une
/// configuration de passerelle qui énumère les routes, par exemple. C'est un <em>constat</em>, jamais une
/// modification : l'outil ne sait pas ce que cette mention signifie, et la corriger serait réécrire une
/// donnée dont il ignore la grammaire. Le dire laisse la décision à qui la comprend.
/// </para>
/// </remarks>
public static class ExtensionCarryOver
{
    /// <summary>L'identifiant du constat, qui fait partie de la sortie.</summary>
    public const string RuleId = "AUD-304";

    private const string Prefix = "x-";

    /// <summary>Les extensions du niveau du document, à emporter telles quelles.</summary>
    /// <param name="root">La racine de la source.</param>
    /// <returns>Les entrées à recopier.</returns>
    public static IReadOnlyList<MappingEntry> DocumentLevel(MappingNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return
        [
            .. root.Entries.Where(entry =>
                entry.Key.Text.StartsWith(Prefix, StringComparison.Ordinal)),
        ];
    }

    /// <summary>
    /// Les extensions emportées qui nomment un chemin absent de l'extrait.
    /// </summary>
    /// <param name="extract">L'extrait monté.</param>
    /// <param name="selectors">Les opérations retenues.</param>
    /// <returns>Un constat par extension concernée.</returns>
    public static IReadOnlyList<Finding> Dangling(
        SpecNode extract,
        IReadOnlyList<OperationSelector> selectors)
    {
        ArgumentNullException.ThrowIfNull(extract);
        ArgumentNullException.ThrowIfNull(selectors);

        HashSet<string> kept = new(
            selectors.Select(selector => selector.Path), StringComparer.Ordinal);

        List<Finding> found = [];

        foreach ((SpecPointer location, string name, IReadOnlyList<string> mentioned) in Mentions(
            extract))
        {
            IReadOnlyList<string> absent =
            [
                .. mentioned.Where(path => !kept.Contains(path)).Distinct(StringComparer.Ordinal),
            ];

            if (absent.Count == 0)
            {
                continue;
            }

            found.Add(Report(location, name, absent));
        }

        return found;
    }

    /// <summary>
    /// Les chemins qu'une extension mentionne, repérés par leur forme.
    /// </summary>
    /// <remarks>
    /// Par leur forme — une chaîne commençant par une barre oblique — et non par une grammaire. L'outil
    /// n'en connaît aucune pour une extension, et une reconnaissance trop sûre d'elle ferait d'une URL ou
    /// d'un chemin de fichier une route manquante. La conséquence d'un faux positif est ici un constat
    /// de trop, que le relecteur écarte ; celle d'un faux négatif est une configuration de passerelle
    /// qui désigne le vide.
    /// </remarks>
    private static IEnumerable<(SpecPointer Location, string Name, IReadOnlyList<string> Paths)>
        Mentions(SpecNode node)
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
                if (!entry.Key.Text.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    pending.Push(entry.Value);
                    continue;
                }

                if (Templates(entry.Value) is { Count: > 0 } paths)
                {
                    yield return (entry.Value.Location, entry.Key.Text, paths);
                }
            }
        }
    }

    /// <summary>Les chaînes d'un sous-arbre qui ont la forme d'un gabarit de chemin.</summary>
    private static List<string> Templates(SpecNode node)
    {
        List<string> found = [];
        Stack<SpecNode> pending = new();
        pending.Push(node);

        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case ScalarNode scalar when scalar.Text.StartsWith('/'):
                    found.Add(scalar.Text);
                    break;

                case MappingNode mapping:
                    foreach (MappingEntry entry in mapping.Entries)
                    {
                        if (entry.Key.Text.StartsWith('/'))
                        {
                            found.Add(entry.Key.Text);
                        }

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

        return found;
    }

    private static Finding Report(
        SpecPointer location,
        string name,
        IReadOnlyList<string> absent) =>
        new(
            RuleId,
            FindingSeverity.Warning,
            SafetyClass.Unsafe,
            "L'extension « " + name + " » emportée dans l'extrait nomme "
            + string.Join(", ", absent.Select(path => "« " + path + " »"))
            + ", que l'extrait ne contient pas.",
            location,
            "Décider si cette extension doit être ajustée pour l'extrait. L'outil ne la modifie pas : "
            + "il ne connaît pas sa grammaire.")
        {
            Evidence = new FindingEvidence(
                Reasoning: "Repéré d'après la forme des chaînes, non d'après une grammaire connue de "
                    + "cette extension."),
        };
}
