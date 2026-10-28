using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Correction;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>
/// Demande ce qui doit entrer en production, puis énonce ce qui va être inséré.
/// </summary>
/// <remarks>
/// Chaque ligne porte l'effet et la confiance que la comparaison a établis, parce que c'est la seule chose
/// qu'elle existe pour produire : choisir sans les voir reviendrait à choisir à l'aveugle, et l'outil
/// aurait travaillé pour rien.
/// <para>
/// Les routes que la production porte déjà à l'identique sont montrées, grisées et non cochables. Les
/// cacher serait plus propre à l'œil et pire à l'usage : un développeur qui cherche une route et ne la
/// trouve pas ne peut pas distinguer « elle correspondait déjà » de « l'outil l'a manquée », et seul
/// l'outil sait lequel des deux c'est.
/// </para>
/// </remarks>
public sealed class CorrectionPicker
{
    private const int PageSize = 15;

    private readonly IAnsiConsole _console;

    /// <summary>Crée le sélecteur.</summary>
    /// <param name="console">La console où demander.</param>
    public CorrectionPicker(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        _console = console;
    }

    /// <summary>Demande ce qu'il faut porter en production.</summary>
    /// <param name="routes">Les routes du candidat, celles qui n'apportent rien comprises.</param>
    /// <returns>Ce que le développeur retient, vide s'il renonce.</returns>
    public CorrectionSelection Ask(IReadOnlyList<CorrectionRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        List<CorrectionRoute> carryable =
            [.. routes.Where(route => !route.NothingToCarry)];

        Announce(routes, carryable);

        if (carryable.Count == 0)
        {
            return new CorrectionSelection();
        }

        List<string> chosen = Pick(carryable);

        if (chosen.Count == 0)
        {
            _console.MarkupLine("[yellow]Rien n'a été retenu : aucune production consolidée ne sera écrite.[/]");

            return new CorrectionSelection();
        }

        return Resolve(carryable, chosen);
    }

    /// <summary>Énonce ce qui va être inséré, et demande l'accord.</summary>
    /// <param name="plan">Ce qui serait inséré, élément par élément.</param>
    /// <returns><see langword="true"/> si le développeur accepte.</returns>
    public bool Confirm(CorrectionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Ce qui sera inséré")
            .AddColumn("Effet");

        foreach (OperationSelector route in plan.Routes)
        {
            _ = table.AddRow(Markup.Escape(route.Display), "route neuve");
        }

        foreach (PlannedDifference difference in plan.Differences)
        {
            _ = table.AddRow(Markup.Escape(difference.Display), Effect(difference.Impact));
        }

        _console.Write(table);

        if (plan.Components > 0)
        {
            _console.MarkupLine(
                CultureInfo.InvariantCulture,
                "Plus les composants dont ces éléments ont besoin : [bold]{0}[/].",
                plan.Components.ToString(CultureInfo.InvariantCulture));
        }

        Derived(plan);

        foreach (DeclinedCorrection declined in plan.Declined)
        {
            _console.MarkupLine(
                CultureInfo.InvariantCulture,
                "[yellow]Écarté — {0} : {1}[/]",
                Markup.Escape(declined.What),
                Markup.Escape(declined.Why));
        }

        Incompatibility(plan);

        _console.MarkupLine(
            "Votre fichier de production ne sera [bold]pas[/] modifié : un fichier consolidé distinct "
            + "sera écrit à côté des rapports.");

        return _console.Confirm("Écrire la production consolidée de ces éléments ?", defaultValue: false);
    }

    /// <summary>Dit qu'au moins un changement rompt la compatibilité, et ce que la version en fait.</summary>
    /// <remarks>
    /// Dire le fait, non la conséquence. « Casse les appelants existants » était faux dès lors que le
    /// contrat consolidé est publié sous un nouveau numéro de version : les appelants de la version en
    /// service continuent de l'appeler, inchangée. Ce que l'outil sait — et c'est tout ce qu'il sait —
    /// c'est que le contrat cesse d'être compatible avec le précédent, donc que le saut de version est
    /// majeur. Annoncer une panne chez des tiers quand on ne connaît ni leur version ni leur déploiement,
    /// c'est exactement l'alarme que son lecteur apprend à ignorer.
    /// </remarks>
    private void Incompatibility(CorrectionPlan plan)
    {
        if (!plan.CarriesBreaking)
        {
            return;
        }

        _console.MarkupLine(plan.Version?.Effective is { } next
            ? "[yellow]Au moins un de ces changements rend le contrat incompatible avec le précédent.[/] "
                + "C'est pourquoi la version passe à [bold]" + Markup.Escape(next.ToString())
                + "[/] : un client écrit pour l'ancien contrat doit être adapté avant d'appeler celui-ci, "
                + "et celui qui appelle la version en service n'est pas touché."
            : "[yellow]Au moins un de ces changements rend le contrat incompatible avec le précédent.[/] "
                + "Un client écrit pour l'ancien contrat doit être adapté avant d'appeler celui-ci.");
    }

    /// <summary>
    /// Ce qu'un candidat projeté ajoute à l'énoncé : les chemins déjà portés, et ce qui restera vide.
    /// </summary>
    /// <remarks>
    /// Dit avant l'accord, et non en ouvrant le fichier. Un développeur qui apprend après coup que trois
    /// endroits du document qu'il vient de faire écrire ne portent rien l'apprend trop tard pour
    /// renoncer, et c'est le moment de renoncer qui compte.
    /// </remarks>
    private void Derived(CorrectionPlan plan)
    {
        foreach (PathCollision collision in plan.Collisions)
        {
            _console.MarkupLine(
                CultureInfo.InvariantCulture,
                "[yellow]Chemin déjà porté — {0}[/]",
                Markup.Escape(collision.Display));
        }

        if (plan.Unfilled.Count == 0)
        {
            return;
        }

        _console.MarkupLine(
            CultureInfo.InvariantCulture,
            "[yellow]{0} endroit(s) resteront non remplis[/] : la projection n'y a rien porté, et "
            + "l'outil refuse de les deviner. Le rapport les nomme un par un.",
            plan.Unfilled.Count.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Dit ce que la comparaison a trouvé, et ce qui n'apporte rien.</summary>
    private void Announce(IReadOnlyList<CorrectionRoute> routes, List<CorrectionRoute> carryable)
    {
        int identical = routes.Count - carryable.Count;

        _console.MarkupLine(
            CultureInfo.InvariantCulture,
            "Le candidat porte [bold]{0}[/] route(s), dont [bold]{1}[/] apportent quelque chose.",
            routes.Count.ToString(CultureInfo.InvariantCulture),
            carryable.Count.ToString(CultureInfo.InvariantCulture));

        if (identical > 0)
        {
            _console.MarkupLine(
                CultureInfo.InvariantCulture,
                "[grey]{0} route(s) sont déjà identiques en production : rien à y reporter.[/]",
                identical.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Présente les éléments cochables, groupés par route.</summary>
    private List<string> Pick(List<CorrectionRoute> carryable)
    {
        MultiSelectionPrompt<string> prompt = new MultiSelectionPrompt<string>()
            .Title("Que faut-il ajouter au fichier de production ?")
            .PageSize(PageSize)
            .MoreChoicesText("[grey](flèches pour parcourir)[/]")
            .InstructionsText(
                "[grey](espace pour cocher, entrée pour valider ; cocher une route la porte entière)[/]")
            .NotRequired();

        foreach (CorrectionRoute route in carryable)
        {
            string header = Label(route);

            if (route.State is RouteState.Absent || route.Differences.Count == 0)
            {
                _ = prompt.AddChoice(header);

                continue;
            }

            _ = prompt.AddChoiceGroup(
                header,
                [.. route.Differences.Select(difference => Label(route, difference))]);
        }

        return [.. _console.Prompt(prompt)];
    }

    /// <summary>Retrouve, derrière les lignes cochées, les routes et les différences retenues.</summary>
    private static CorrectionSelection Resolve(
        List<CorrectionRoute> carryable, List<string> chosen)
    {
        HashSet<string> picked = new(chosen, StringComparer.Ordinal);
        List<OperationSelector> routes = [];
        List<DifferenceSelector> differences = [];

        foreach (CorrectionRoute route in carryable)
        {
            if (picked.Contains(Label(route)) && route.State is RouteState.Absent)
            {
                routes.Add(route.Selector);

                continue;
            }

            differences.AddRange(route.Differences
                .Where(difference => picked.Contains(Label(route, difference)))
                .Select(difference => new DifferenceSelector(
                    route.Selector, difference.ChangeId, difference.Location.Json)));
        }

        return new CorrectionSelection(routes, differences);
    }

    private static string Label(CorrectionRoute route) => Markup.Escape(route.Display);

    private static string Label(CorrectionRoute route, Difference difference) =>
        Markup.Escape(
            route.Selector.Display + " · " + difference.ChangeId + " · "
            + difference.Location.Display + " — " + Plain(difference.Impact) + ", "
            + Plain(difference.Confidence) + route.Footing);

    private static string Effect(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking => "[yellow]incompatible avec l'ancien contrat[/]",
        ChangeImpact.NonBreaking => "[green]compatible[/]",
        ChangeImpact.DocumentationOnly => "[grey]documentation[/]",
        _ => "[yellow]indéterminé[/]",
    };

    private static string Plain(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking => "incompatible",
        ChangeImpact.NonBreaking => "compatible",
        ChangeImpact.DocumentationOnly => "documentaire",
        _ => "indéterminé",
    };

    private static string Plain(Confidence confidence) => confidence switch
    {
        Confidence.Certain => "certain",
        Confidence.Probable => "probable",
        _ => "incertain",
    };
}
