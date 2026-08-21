using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Comparison;
using DevToolbox.Tools.VarCompare.Core.Groups;
using Spectre.Console;

namespace DevToolbox.Tools.VarCompare.Presentation.Prompts;

/// <summary>
/// Retient les groupes de variables à comparer, avec un filtre sur le nom et un minimum de deux groupes.
/// </summary>
/// <remarks>
/// Une sélection de moins de deux groupes est expliquée puis redemandée en conservant les choix déjà faits,
/// plutôt que de renvoyer le développeur au point de départ.
/// </remarks>
public sealed class GroupSelectionPrompt : IGroupChooser
{
    private const string NoFilter = "";

    private readonly IAnsiConsole _console;

    /// <summary>Crée l'invite.</summary>
    /// <param name="console">La console où présenter l'invite.</param>
    public GroupSelectionPrompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<VariableGroupSummary>> ChooseAsync(
        IReadOnlyList<VariableGroupSummary> available,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(available);

        IReadOnlyList<VariableGroupSummary> candidates = ApplyFilter(available);
        List<VariableGroupSummary> selected = [];

        while (!cancellationToken.IsCancellationRequested)
        {
            selected = Ask(candidates, selected);

            if (selected.Count >= VariableComparison.MinimumGroups)
            {
                return Task.FromResult<IReadOnlyList<VariableGroupSummary>>(selected);
            }

            _console.MarkupLine(
                "[yellow]Choisissez au moins deux groupes : une comparaison a besoin d'un point de "
                + "comparaison.[/]");

            if (!_console.Confirm("Réessayer ?"))
            {
                return Task.FromResult<IReadOnlyList<VariableGroupSummary>>([]);
            }
        }

        return Task.FromResult<IReadOnlyList<VariableGroupSummary>>([]);
    }

    private IReadOnlyList<VariableGroupSummary> ApplyFilter(IReadOnlyList<VariableGroupSummary> available)
    {
        // Le filtre ne se justifie qu'à partir du moment où la liste devient malcommode.
        const int filterThreshold = 12;

        if (available.Count <= filterThreshold)
        {
            return available;
        }

        string filter = _console.Prompt(
            new TextPrompt<string>($"Filtrer les {available.Count} groupes par nom (laissez vide pour tous) :")
                .AllowEmpty()
                .DefaultValue(NoFilter)
                .HideDefaultValue());

        if (string.IsNullOrWhiteSpace(filter))
        {
            return available;
        }

        List<VariableGroupSummary> matches =
        [
            .. available.Where(group =>
                group.Name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)),
        ];

        if (matches.Count == 0)
        {
            _console.MarkupLine("[yellow]Aucun groupe ne correspond à ce filtre ; ils sont tous affichés.[/]");
            return available;
        }

        return matches;
    }

    private List<VariableGroupSummary> Ask(
        IReadOnlyList<VariableGroupSummary> candidates,
        IReadOnlyList<VariableGroupSummary> previouslySelected)
    {
        Dictionary<string, VariableGroupSummary> byLabel = new(StringComparer.Ordinal);

        foreach (VariableGroupSummary group in candidates)
        {
            byLabel[Label(group)] = group;
        }

        MultiSelectionPrompt<string> prompt = new MultiSelectionPrompt<string>()
            .Title("Quels [bold]groupes[/] voulez-vous comparer ?")
            .PageSize(15)
            .MoreChoicesText("[grey](déplacez-vous vers le haut ou le bas pour en voir plus)[/]")
            .InstructionsText("[grey](espace pour cocher, Entrée pour valider)[/]")
            .AddChoices(byLabel.Keys);

        // Garder cochés les choix déjà faits, pour qu'une sélection refusée se corrige au lieu de se refaire.
        foreach (VariableGroupSummary group in previouslySelected)
        {
            string label = Label(group);

            if (byLabel.ContainsKey(label))
            {
                prompt.Select(label);
            }
        }

        List<string> chosen = _console.Prompt(prompt);

        return [.. chosen.Where(byLabel.ContainsKey).Select(label => byLabel[label])];
    }

    /// <summary>
    /// Le libellé sous lequel un groupe est choisi. Il porte l'id afin que deux groupes de même nom restent
    /// distinguables.
    /// </summary>
    private static string Label(VariableGroupSummary group) =>
        group.IsKeyVaultBacked
            ? $"{group.Name}  (n° {group.Id}, coffre de clés)"
            : $"{group.Name}  (n° {group.Id})";
}
