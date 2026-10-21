using DevToolbox.Presentation.Shell;
using DevToolbox.Tools.ApiDelta.Core.Audit;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Rendering;

/// <summary>
/// Montre ce que le nettoyage rapporterait, en séparant ce que l'outil sait prouver de ce qu'il faut
/// relire.
/// </summary>
/// <remarks>
/// La séparation est le propos de cet écran. Une liste unique obligerait à vérifier chaque ligne pour
/// savoir laquelle des deux on regarde, et c'est ainsi qu'une transformation à relire finit par être
/// appliquée parce qu'elle était au milieu des autres.
/// <para>
/// Rien ici ne modifie quoi que ce soit, et l'écran le dit : le plan est un devis. L'application arrive
/// avec la tranche suivante, et elle demandera sa propre confirmation.
/// </para>
/// </remarks>
public sealed class ProposalsView
{
    private const string SafeChoice = "Voir ce qui est prouvé sûr";

    private const string ReviewChoice = "Voir ce qui demande une relecture";

    private const string GroupsChoice = "Voir toutes les répétitions mesurées";

    private readonly IAnsiConsole _console;

    /// <summary>Crée la vue.</summary>
    /// <param name="console">La console où écrire.</param>
    public ProposalsView(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <summary>Affiche le chiffrage, puis propose d'entrer dans le détail.</summary>
    /// <param name="plan">Ce que le nettoyage rapporterait.</param>
    /// <param name="currentBytes">Ce que le fichier pèse aujourd'hui.</param>
    /// <param name="applied">Ce que l'application a produit, quand elle a eu lieu.</param>
    /// <param name="cancellationToken">Annule la navigation.</param>
    public void Show(
        OptimizationPlan plan,
        long currentBytes,
        OptimizationOutcome? applied,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        RenderSummary(plan, currentBytes);
        RenderApplied(applied);

        if (plan.Groups.Count == 0)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            string chosen = _console.Prompt(
                new SelectionPrompt<string>()
                    .Title("Que souhaitez-vous regarder ?")
                    .AddChoices(SafeChoice, ReviewChoice, GroupsChoice, NavigationLabels.Back));

            if (string.Equals(chosen, NavigationLabels.Back, StringComparison.Ordinal))
            {
                return;
            }

            Render(plan, chosen);
        }
    }

    private void Render(OptimizationPlan plan, string chosen)
    {
        if (string.Equals(chosen, SafeChoice, StringComparison.Ordinal))
        {
            RenderProposals(
                [.. plan.Safe],
                "Prouvé sûr",
                "Rien de ce qui a été trouvé ne se corrige sans relecture.");

            return;
        }

        if (string.Equals(chosen, ReviewChoice, StringComparison.Ordinal))
        {
            RenderProposals(
                [.. plan.ReviewRequired],
                "À relire avant d'appliquer",
                "Aucune transformation ne demande de relecture.");

            return;
        }

        RenderGroups(plan);
    }

    /// <summary>Le panneau : ce que le fichier pèse, ce qu'il pèserait, et à quel titre.</summary>
    private void RenderSummary(OptimizationPlan plan, long currentBytes)
    {
        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn();

        grid.AddRow("[grey]répétitions mesurées[/]", Count(plan.Groups.Count));
        grid.AddRow("[grey]propositions prouvées sûres[/]", Count(plan.Safe.Count()));
        grid.AddRow("[grey]propositions à relire[/]", Count(plan.ReviewRequired.Count()));
        grid.AddRow("[grey]taille actuelle[/]", Bytes(currentBytes) + " octets");
        grid.AddRow("[grey]économie estimée[/]", Bytes(plan.TotalEstimatedSaving) + " octets");
        grid.AddRow("[grey]taille projetée[/]", Bytes(plan.Growth.ProjectedBytes) + " octets");

        _console.Write(new Panel(grid)
            .Header("[bold]Ce que le nettoyage rapporterait[/]")
            .Border(BoxBorder.Rounded));

        _console.MarkupLine(
            "[grey]Ce chiffrage est un devis : aucun fichier n'a été modifié.[/]");

        if (plan.Growth.Exceeded)
        {
            _console.MarkupLine(
                "[yellow]La sortie dépasserait la croissance tolérée : rien ne serait publié.[/]");
        }

        RenderUncovered(plan);

        _console.WriteLine();
    }

    /// <summary>
    /// Dit ce qui a été appliqué, ce qui a été refusé, et si la copie a pu être publiée.
    /// </summary>
    /// <remarks>
    /// L'échec de vérification est annoncé ici, au premier écran, et non laissé à lire dans un rapport.
    /// Un développeur qui a demandé une application et à qui l'on ne dit rien suppose qu'elle a eu lieu.
    /// </remarks>
    private void RenderApplied(OptimizationOutcome? applied)
    {
        if (applied is null)
        {
            return;
        }

        _console.MarkupLine(
            "[grey]transformations appliquées : " + Count(applied.Applied.Count) + "[/]");

        foreach (DeclinedTransformation declined in applied.Declined)
        {
            _console.MarkupLine(
                "[yellow]écartée :[/] " + Markup.Escape(declined.Transformation.RuleId)
                + " [grey]— " + Markup.Escape(declined.Reason) + "[/]");
        }

        _console.MarkupLine(applied.Verification.Established
            ? "[green]Copie vérifiée :[/] [grey]"
                + Markup.Escape(applied.Verification.Explanation) + "[/]"
            : "[red]Rien n'a été publié :[/] [grey]"
                + Markup.Escape(applied.Verification.Explanation) + "[/]");

        _console.WriteLine();
    }

    /// <summary>
    /// Dit, sur le panneau, combien de répétitions réelles ne sont couvertes par aucune proposition.
    /// </summary>
    /// <remarks>
    /// Un plan sans proposition ne veut pas dire un fichier sans répétition. Deux schémas identiques sous
    /// deux noms, deux énumérations aux mêmes valeurs : l'outil les a trouvés et ne sait pas les traiter.
    /// Le taire laisserait croire qu'il n'y avait rien.
    /// </remarks>
    private void RenderUncovered(OptimizationPlan plan)
    {
        int uncovered = plan.Uncovered.Count();

        if (uncovered == 0)
        {
            return;
        }

        _console.MarkupLine(
            "[yellow]" + Count(uncovered)
            + " répétition(s) mesurée(s) ne sont couvertes par aucune proposition :[/] [grey]soit "
            + "l'extraction coûterait plus qu'elle ne rapporte, soit l'outil ne sait pas établir que "
            + "le remède laisse les appelants inchangés.[/]");
    }

    private void RenderProposals(
        IReadOnlyList<Transformation> proposals,
        string title,
        string empty)
    {
        _console.Write(new Rule("[bold]" + Markup.Escape(title) + "[/]").LeftJustified());

        if (proposals.Count == 0)
        {
            _console.MarkupLine("[grey]" + Markup.Escape(empty) + "[/]");
            _console.WriteLine();
            return;
        }

        foreach (Transformation proposal in proposals
            .OrderByDescending(proposal => proposal.EstimatedBytesSaved)
            .ThenBy(proposal => proposal.RuleId, StringComparer.Ordinal))
        {
            RenderProposal(proposal);
        }

        _console.WriteLine();
    }

    private void RenderProposal(Transformation proposal)
    {
        _console.MarkupLine(
            "[bold]" + Markup.Escape(proposal.RuleId) + "[/] "
            + Markup.Escape(proposal.Summary));

        _console.MarkupLine(
            "  [grey]remplace " + Count(proposal.Origins.Count) + " écriture(s), à partir de "
            + Markup.Escape(proposal.Origins[0].Json) + "[/]");

        if (proposal.Destination is { } destination)
        {
            _console.MarkupLine("  [grey]crée " + Markup.Escape(destination.Json) + "[/]");
        }

        _console.MarkupLine(
            "  [grey]économie estimée : " + Bytes(proposal.EstimatedBytesSaved) + " octets"
            + (proposal.BreakEvenOccurrences is { } breakEven
                ? " · rentable à partir de " + Count(breakEven) + " occurrences"
                : string.Empty)
            + "[/]");

        if (proposal.ReviewReason is { } reason)
        {
            _console.MarkupLine("  [yellow]à relire :[/] [grey]" + Markup.Escape(reason) + "[/]");
        }

        _console.MarkupLine(
            "  [grey]pour la défaire : " + Markup.Escape(proposal.Reversal.Text) + "[/]");
    }

    /// <summary>Toutes les répétitions mesurées, y compris celles qui ne valent pas d'être traitées.</summary>
    private void RenderGroups(OptimizationPlan plan)
    {
        Table table = new();
        table.AddColumns("Règle", "Niveau", "Occurrences", "Économie", "Seuil", "Sûreté");

        foreach (RedundancyGroup group in plan.Groups)
        {
            table.AddRow(
                Markup.Escape(group.RuleId),
                group.Tier == DuplicateTier.Exact ? "identique" : "voisin",
                Count(group.Count),
                Bytes(group.Savings.AbsoluteSaving),
                group.Savings.BreakEvenOccurrences is { } breakEven ? Count(breakEven) : "jamais",
                Safety(group.Safety));
        }

        _console.Write(table);
        _console.WriteLine();
    }

    private static string Count(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Bytes(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Safety(SafetyClass safety) => safety switch
    {
        SafetyClass.Safe => "prouvé sûr",
        SafetyClass.ReviewRequired => "à relire",
        _ => "signalé seulement",
    };
}
