using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Rendering;

/// <summary>
/// Montre ce que l'examen a trouvé : d'abord le compte, ensuite le détail règle par règle.
/// </summary>
/// <remarks>
/// Les constats sont groupés par règle parce que c'est la décision qui est l'unité utile : quatorze
/// occurrences d'un même en-tête se tranchent une fois.
/// <para>
/// Les règles qui n'ont pas pu être évaluées sont annoncées sur le panneau, avec le compte. Les enterrer
/// dans un sous-écran laisserait un lecteur croire qu'un examen sans constat est un examen propre, ce qui
/// est exactement l'erreur que cet outil existe pour ne pas commettre.
/// </para>
/// </remarks>
public sealed class FindingsView
{
    private const int PageSize = 10;

    private const string Everything = "Voir tous les constats, un par un";

    private const string ByRule = "Choisir une règle à regarder";

    private readonly IAnsiConsole _console;

    /// <summary>Crée la vue.</summary>
    /// <param name="console">La console où écrire.</param>
    public FindingsView(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Affiche le résumé de l'examen, puis propose d'entrer dans le détail.</summary>
    /// <param name="audit">Ce que l'examen a constaté.</param>
    /// <param name="cancellationToken">Annule la navigation.</param>
    public void Show(AuditResult audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);

        RenderSummary(audit);

        if (audit.Findings.Count == 0)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            string chosen = _console.Prompt(
                new SelectionPrompt<string>()
                    .Title("Que souhaitez-vous regarder ?")
                    .AddChoices(Everything, ByRule, NavigationLabels.Back));

            if (string.Equals(chosen, NavigationLabels.Back, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(chosen, Everything, StringComparison.Ordinal))
            {
                RenderPaged(audit.Findings, "Tous les constats", cancellationToken);
                continue;
            }

            if (!RenderRule(audit, cancellationToken))
            {
                return;
            }
        }
    }

    /// <summary>Le panneau : combien de constats, de quelle gravité, et ce qui n'a pas été regardé.</summary>
    private void RenderSummary(AuditResult audit)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        foreach (FindingSeverity severity in new[]
        {
            FindingSeverity.Error, FindingSeverity.Warning,
            FindingSeverity.Information, FindingSeverity.Opportunity,
        })
        {
            int count = audit.Findings.Count(finding => finding.Severity == severity);

            grid.AddRow(
                "[grey]" + Label(severity) + "[/]",
                count == 0 ? "[grey]0[/]" : Emphasise(severity, count));
        }

        long saved = audit.Findings.Sum(finding => finding.EstimatedBytesSaved ?? 0);

        if (saved > 0)
        {
            grid.AddRow(
                "[grey]octets qu'un nettoyage ferait gagner[/]",
                saved.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        _console.Write(new Panel(grid)
            .Header("[bold]Ce que le fichier de production a accumulé[/]")
            .Border(BoxBorder.Rounded));

        RenderIncomplete(audit);

        _console.WriteLine();
    }

    /// <summary>
    /// Dit, sur le panneau, que l'examen n'est pas complet.
    /// </summary>
    /// <remarks>
    /// Une règle qui n'a pas tourné n'a rien trouvé, et cela ne veut pas dire qu'il n'y a rien. C'est la
    /// discipline de l'outil : indéterminé n'est pas absent, et cela se lit au premier écran.
    /// </remarks>
    private void RenderIncomplete(AuditResult audit)
    {
        IReadOnlyList<RuleOutcome> missing = [.. audit.NotEvaluated];
        IReadOnlyList<RuleOutcome> partial = [.. audit.Truncated];

        foreach (RuleOutcome outcome in missing)
        {
            _console.MarkupLine(
                "[yellow]non évaluée :[/] " + Markup.Escape(outcome.RuleId) + " [grey]— "
                + Markup.Escape(outcome.Reason ?? "sans raison consignée") + "[/]");
        }

        foreach (RuleOutcome outcome in partial)
        {
            _console.MarkupLine(
                "[yellow]rendue incomplète :[/] " + Markup.Escape(outcome.RuleId) + " [grey]— "
                + Markup.Escape(outcome.Reason ?? "sans raison consignée") + "[/]");
        }

        if (missing.Count > 0)
        {
            _console.MarkupLine(
                "[yellow]Cet examen n'est pas complet : ce que ces règles auraient trouvé reste "
                + "inconnu.[/]");
        }
    }

    /// <summary>Propose une règle, puis en montre les constats.</summary>
    /// <returns><see langword="false"/> si le développeur veut revenir en arrière.</returns>
    private bool RenderRule(AuditResult audit, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> rules =
        [
            .. audit.Findings
                .Select(finding => finding.RuleId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(rule => rule, StringComparer.Ordinal),
        ];

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Quelle règle ?")
                .PageSize(Math.Max(3, Math.Min(rules.Count + 1, 20)))
                .AddChoices([.. rules, NavigationLabels.Back]));

        if (string.Equals(chosen, NavigationLabels.Back, StringComparison.Ordinal))
        {
            return false;
        }

        RenderPaged(
            [.. audit.Findings.Where(finding =>
                string.Equals(finding.RuleId, chosen, StringComparison.Ordinal))],
            chosen,
            cancellationToken);

        return true;
    }

    private void RenderPaged(
        IReadOnlyList<Finding> findings,
        string title,
        CancellationToken cancellationToken)
    {
        int page = 0;
        int pages = (findings.Count + PageSize - 1) / PageSize;

        while (!cancellationToken.IsCancellationRequested)
        {
            _console.Write(new Rule("[bold]" + Markup.Escape(title) + "[/]").LeftJustified());

            foreach (Finding finding in findings.Skip(page * PageSize).Take(PageSize))
            {
                Render(finding);
            }

            _console.MarkupLine($"[grey]page {page + 1} / {pages}[/]");

            if (!Turn(page, pages, out int next))
            {
                return;
            }

            page = next;
        }
    }

    /// <summary>Demande la page suivante, la précédente, ou la sortie.</summary>
    private bool Turn(int page, int pages, out int next)
    {
        List<string> choices = [];

        if (page + 1 < pages)
        {
            choices.Add("Page suivante");
        }

        if (page > 0)
        {
            choices.Add("Page précédente");
        }

        choices.Add(NavigationLabels.Back);

        string chosen = _console.Prompt(new SelectionPrompt<string>().AddChoices(choices));

        next = chosen switch
        {
            "Page suivante" => page + 1,
            "Page précédente" => page - 1,
            _ => page,
        };

        return !string.Equals(chosen, NavigationLabels.Back, StringComparison.Ordinal);
    }

    /// <summary>Écrit un constat : ce qu'il dit, où, et ce qu'il coûterait de le corriger.</summary>
    private void Render(Finding finding)
    {
        _console.MarkupLine(
            Emphasise(finding.Severity, Tag(finding.Severity)) + " "
            + Markup.Escape(finding.Message));

        _console.MarkupLine("  [grey]" + Markup.Escape(finding.Location.Display) + "[/]");

        if (finding.Locations.Count > 1)
        {
            _console.MarkupLine(
                "  [grey]et " + (finding.Locations.Count - 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                + " autre(s) emplacement(s)[/]");
        }

        _console.MarkupLine("  " + Markup.Escape(finding.RecommendedAction));

        if (finding.EstimatedBytesSaved is { } saved)
        {
            _console.MarkupLine(
                "  [grey]économie estimée : "
                + saved.ToString(System.Globalization.CultureInfo.InvariantCulture) + " octets"
                + (finding.AutofixAvailable ? " · corrigeable automatiquement" : string.Empty) + "[/]");
        }

        if (finding.Evidence?.Reasoning is { } reasoning)
        {
            _console.MarkupLine("  [yellow]déduction :[/] [grey]" + Markup.Escape(reasoning) + "[/]");
        }

        _console.MarkupLine(
            "  [grey]" + Markup.Escape(finding.Location.Json) + " · "
            + Markup.Escape(finding.RuleId) + " · " + Safety(finding.Safety) + "[/]");
    }

    /// <summary>La forme courte d'une gravité, pour tenir en tête de ligne.</summary>
    private static string Tag(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Error => "ERREUR",
        FindingSeverity.Warning => "à corriger",
        FindingSeverity.Information => "information",
        _ => "occasion",
    };

    private static string Label(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Error => "erreurs",
        FindingSeverity.Warning => "à corriger",
        FindingSeverity.Information => "pour information",
        _ => "occasions de simplifier",
    };

    private static string Safety(SafetyClass safety) => safety switch
    {
        SafetyClass.Safe => "remède prouvé sûr",
        SafetyClass.ReviewRequired => "remède à relire",
        _ => "signalé seulement",
    };

    /// <summary>
    /// Met en valeur sans que la couleur porte seule le sens : le mot reste lisible sans couleurs.
    /// </summary>
    private static string Emphasise(FindingSeverity severity, object value) => severity switch
    {
        FindingSeverity.Error => "[red]" + Markup.Escape(value.ToString() ?? string.Empty) + "[/]",
        FindingSeverity.Warning => "[yellow]" + Markup.Escape(value.ToString() ?? string.Empty) + "[/]",
        _ => "[grey]" + Markup.Escape(value.ToString() ?? string.Empty) + "[/]",
    };
}
