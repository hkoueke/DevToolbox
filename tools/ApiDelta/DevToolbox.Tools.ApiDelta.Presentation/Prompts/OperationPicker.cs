using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>
/// Demande quelles opérations extraire : filtrer d'abord, cocher ensuite.
/// </summary>
/// <remarks>
/// Filtrer d'abord, et ce n'est pas une préférence. Une description de quatre cents opérations n'est pas
/// une liste qu'on parcourt : la présenter telle quelle demanderait au développeur de retrouver à l'œil
/// deux lignes parmi quatre cents, ce qui est exactement le travail qu'il venait éviter. L'étiquette ou
/// un fragment de chemin ramènent la liste à une poignée, et c'est seulement là que cocher a un sens.
/// <para>
/// La sélection est <em>par opération</em> et non par chemin : on prend <c>GET /documents/{id}</c> sans
/// prendre le <c>DELETE</c> qui vit à côté. C'est tout l'intérêt de ce mode, et un choix par chemin
/// l'aurait annulé.
/// </para>
/// <para>
/// Spectre.Console 0.57.2 n'offre pas de recherche incrémentale dans
/// <see cref="MultiSelectionPrompt{T}"/> : l'étape de filtre n'est donc pas une commodité mais le seul
/// moyen de rendre la liste praticable. Elle resterait nécessaire même si la recherche existait, car un
/// filtre dit ce qu'il a retenu alors qu'une recherche ne montre que ce qu'elle trouve.
/// </para>
/// </remarks>
public sealed class OperationPicker
{
    private const int PageSize = 15;

    private const string ByTag = "Filtrer par étiquette";

    private const string ByPath = "Filtrer par chemin";

    private const string Everything = "Tout parcourir";

    private readonly IAnsiConsole _console;

    /// <summary>Crée le sélecteur.</summary>
    /// <param name="console">La console où demander.</param>
    public OperationPicker(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Demande les opérations à extraire.</summary>
    /// <param name="catalogue">Les opérations que la description porte.</param>
    /// <param name="sourceName">Le nom du fichier, pour le dire à l'écran.</param>
    /// <returns>Les opérations retenues, vides si le développeur renonce.</returns>
    public IReadOnlyList<OperationSelector> Ask(
        IReadOnlyList<SelectableOperation> catalogue,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        if (catalogue.Count == 0)
        {
            _console.MarkupLine(
                "[yellow]Cette description ne porte aucune opération : il n'y a rien à extraire.[/]");

            return [];
        }

        _console.MarkupLine(
            "[grey]" + Count(catalogue.Count) + " opérations dans "
            + Markup.Escape(sourceName) + "[/]");

        IReadOnlyList<SelectableOperation> narrowed = Narrow(catalogue);

        return narrowed.Count == 0 ? [] : Choose(narrowed);
    }

    /// <summary>
    /// Demande ce qu'il faut confirmer avant de construire : ce qui sera emporté, et ce qui ne bougera
    /// pas.
    /// </summary>
    /// <param name="selectors">Les opérations retenues.</param>
    /// <param name="components">Le nombre de composants que la fermeture emporterait.</param>
    /// <returns><see langword="true"/> si le développeur confirme.</returns>
    public bool Confirm(IReadOnlyList<OperationSelector> selectors, int components)
    {
        ArgumentNullException.ThrowIfNull(selectors);

        _console.MarkupLine(
            "[grey]" + Count(selectors.Count) + " opération(s), " + Count(components)
            + " composant(s), les paramètres de chemin hérités et l'exigence de sécurité globale. "
            + "Le fichier source n'est pas modifié.[/]");

        return _console.Prompt(
            new ConfirmationPrompt("Construire l'extrait ?") { DefaultValue = true });
    }

    /// <summary>Ramène la liste à une poignée, par étiquette ou par fragment de chemin.</summary>
    private IReadOnlyList<SelectableOperation> Narrow(IReadOnlyList<SelectableOperation> catalogue)
    {
        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Extraire quelles opérations ?")
                .AddChoices(ByTag, ByPath, Everything, NavigationLabels.Back));

        if (string.Equals(chosen, NavigationLabels.Back, StringComparison.Ordinal))
        {
            return [];
        }

        if (string.Equals(chosen, ByTag, StringComparison.Ordinal))
        {
            return Tagged(catalogue);
        }

        return string.Equals(chosen, ByPath, StringComparison.Ordinal)
            ? Matching(catalogue)
            : catalogue;
    }

    /// <summary>Les opérations d'une étiquette, dont le compte est annoncé avant le choix.</summary>
    private IReadOnlyList<SelectableOperation> Tagged(IReadOnlyList<SelectableOperation> catalogue)
    {
        IReadOnlyList<(string Tag, int Count)> groups =
        [
            .. catalogue
                .GroupBy(operation => operation.Group, StringComparer.Ordinal)
                .Select(group => (group.Key, group.Count()))
                .OrderByDescending(group => group.Item2)
                .ThenBy(group => group.Key, StringComparer.Ordinal),
        ];

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Quelle étiquette ?")
                .PageSize(Math.Max(3, Math.Min(groups.Count + 1, PageSize)))
                .AddChoices(
                [
                    .. groups.Select(group => group.Tag + " (" + Count(group.Count) + ")"),
                    NavigationLabels.Back,
                ]));

        if (string.Equals(chosen, NavigationLabels.Back, StringComparison.Ordinal))
        {
            return [];
        }

        string tag = groups
            .First(group => chosen.StartsWith(group.Tag + " (", StringComparison.Ordinal))
            .Tag;

        return
        [
            .. catalogue.Where(operation =>
                string.Equals(operation.Group, tag, StringComparison.Ordinal)),
        ];
    }

    /// <summary>Les opérations dont le chemin contient un fragment.</summary>
    private IReadOnlyList<SelectableOperation> Matching(IReadOnlyList<SelectableOperation> catalogue)
    {
        string fragment = _console.Prompt(
            new TextPrompt<string>("Quelle partie du chemin ?").AllowEmpty());

        if (string.IsNullOrWhiteSpace(fragment))
        {
            return [];
        }

        IReadOnlyList<SelectableOperation> found =
        [
            .. catalogue.Where(operation => operation.Selector.Path.Contains(
                fragment.Trim(), StringComparison.OrdinalIgnoreCase)),
        ];

        if (found.Count == 0)
        {
            _console.MarkupLine(
                "[yellow]Aucun chemin ne contient « " + Markup.Escape(fragment.Trim()) + " ».[/]");
        }

        return found;
    }

    /// <summary>
    /// Coche les opérations, groupées par étiquette.
    /// </summary>
    /// <remarks>
    /// Ne rien cocher et valider est refusé avec une explication : produire un extrait vide laisserait
    /// croire que la sélection avait abouti.
    /// </remarks>
    private IReadOnlyList<OperationSelector> Choose(IReadOnlyList<SelectableOperation> narrowed)
    {
        MultiSelectionPrompt<string> prompt = new()
        {
            Title = "Lesquelles ? [grey](espace pour cocher, entrée pour valider)[/]",
            PageSize = Math.Max(5, Math.Min(narrowed.Count + narrowed.Count / 4 + 2, 25)),
            Required = false,
        };

        Dictionary<string, OperationSelector> rows = new(StringComparer.Ordinal);

        foreach (IGrouping<string, SelectableOperation> group in narrowed
            .GroupBy(operation => operation.Group, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            List<string> children = [];

            foreach (SelectableOperation operation in group)
            {
                string row = Row(operation);

                rows[row] = operation.Selector;
                children.Add(row);
            }

            prompt.AddChoiceGroup(group.Key, children);
        }

        IReadOnlyList<string> chosen = [.. _console.Prompt(prompt)];

        if (chosen.Count == 0)
        {
            _console.MarkupLine(
                "[yellow]Aucune opération n'a été cochée : il n'existe pas d'extrait vide.[/]");

            return [];
        }

        return
        [
            .. chosen
                .Where(rows.ContainsKey)
                .Select(row => rows[row])
                .Distinct(),
        ];
    }

    /// <summary>
    /// La ligne d'une opération : assez pour choisir sans ouvrir le fichier.
    /// </summary>
    /// <remarks>
    /// Le mot « obsolète » en clair plutôt qu'une couleur : une opération dépréciée qu'on extrait par
    /// mégarde est la méprise que cette ligne existe pour éviter, et elle ne doit pas dépendre d'un
    /// terminal qui affiche les couleurs.
    /// </remarks>
    private static string Row(SelectableOperation operation) =>
        operation.Display + (operation.Deprecated ? "  [obsolète]" : string.Empty);

    private static string Count(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
