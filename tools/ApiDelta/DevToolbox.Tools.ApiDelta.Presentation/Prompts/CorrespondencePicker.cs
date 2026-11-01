using DevToolbox.Tools.ApiDelta.Core.Correspondence;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>
/// Montre l'appariement proposé, ligne par ligne, et laisse corriger ou renoncer.
/// </summary>
/// <remarks>
/// Quatre marques, et chacune est une nouvelle différente. Une entrée déclarée vient du développeur et
/// l'outil ne la discute pas ; une entrée proposée montre les jetons qui l'ont justifiée, ce qui rend la
/// correction rapide ; une entrée ambiguë est l'outil qui renonce à trancher, et elle ne se résout jamais
/// en silence ; une entrée absente est un manque, où rien n'est encore cassé.
/// <para>
/// Renoncer est une réponse. La séance réussit, n'ayant rien comparé, et le rapport dit que la
/// correspondance n'a pas été confirmée. Traiter le renoncement comme un échec pousserait un développeur
/// à confirmer ce qu'il n'a pas lu, et c'est précisément le défaut que tout ce mécanisme empêche.
/// </para>
/// </remarks>
public sealed class CorrespondencePicker
{
    private const int PageSize = 15;
    private const string Decline = "Annuler la comparaison";
    private const string Accept = "Valider ces correspondances et comparer";

    private readonly IAnsiConsole _console;

    /// <summary>Crée le sélecteur.</summary>
    /// <param name="console">La console où demander.</param>
    public CorrespondencePicker(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        _console = console;
    }

    /// <summary>Montre l'appariement, puis demande ce qu'il faut en faire.</summary>
    /// <param name="proposed">L'appariement proposé.</param>
    /// <param name="routes">Les routes de la production, pour corriger une ligne.</param>
    /// <returns>L'appariement confirmé, ou le même non confirmé si le développeur renonce.</returns>
    public OperationCorrespondence Ask(
        OperationCorrespondence proposed, IReadOnlyList<string> routes)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(routes);

        if (proposed.Entries.Count == 0)
        {
            _console.MarkupLine(
                "[yellow]Le contrat de service ne déclare aucune opération : il n'y a rien à "
                    + "comparer.[/]");

            return proposed;
        }

        Announce(proposed);

        OperationCorrespondence current = proposed;

        while (true)
        {
            string choice = Pick(current);

            if (string.Equals(choice, Decline, StringComparison.Ordinal))
            {
                _console.MarkupLine(
                    "[yellow]Comparaison annulée. Les fichiers restent inchangés.[/]");

                return current with { IsConfirmed = false };
            }

            if (string.Equals(choice, Accept, StringComparison.Ordinal))
            {
                return current.Confirm();
            }

            current = Correct(current, choice, routes);
        }
    }

    /// <summary>Énonce l'appariement, une ligne par opération.</summary>
    /// <remarks>
    /// Un tableau plutôt qu'une liste cochable, parce que confirmer n'est pas choisir : le développeur
    /// valide un appariement entier ou corrige une ligne, et une case à cocher par ligne suggérerait
    /// qu'on puisse en retenir la moitié — ce qui laisserait comparer sur une base partielle.
    /// </remarks>
    private void Announce(OperationCorrespondence proposed)
    {
        _console.MarkupLine("[bold]Associer les opérations WSDL aux appels OpenAPI[/]");
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("État")
            .AddColumn("Opération")
            .AddColumn("Route")
            .AddColumn("Pourquoi ce choix ?");

        foreach (CorrespondenceEntry entry in proposed.Entries)
        {
            _ = table.AddRow(
                Marked(entry),
                Markup.Escape(entry.Operation),
                Markup.Escape(Route(entry)),
                Markup.Escape(entry.Basis ?? string.Empty));
        }

        _console.Write(table);

        _console.MarkupLine(
            "[grey]Vérifiez que chaque opération du WSDL correspond au bon appel dans votre fichier OpenAPI. Corrigez les lignes incorrectes avant de comparer.[/]");
    }

    /// <summary>La marque d'une entrée, colorée selon la nouvelle qu'elle porte.</summary>
    private static string Marked(CorrespondenceEntry entry) => entry.Origin switch
    {
        CorrespondenceOrigin.Declared => "[green]" + entry.Mark + "[/]",
        CorrespondenceOrigin.Confirmed => "[green]" + entry.Mark + "[/]",
        CorrespondenceOrigin.Proposed => "[cyan]" + entry.Mark + "[/]",
        _ => entry.HasRoute ? "[yellow]" + entry.Mark + "[/]" : "[grey]" + entry.Mark + "[/]",
    };

    /// <summary>La route d'une entrée, les autres candidats compris si elle est ambiguë.</summary>
    private static string Route(CorrespondenceEntry entry)
    {
        if (!entry.HasRoute)
        {
            return "aucun appel correspondant";
        }

        return entry.Alternatives.Count == 0
            ? entry.Route
            : entry.Route + "  ou  " + string.Join("  ou  ", entry.Alternatives);
    }

    /// <summary>Demande quoi faire : confirmer, corriger une ligne, ou renoncer.</summary>
    private string Pick(OperationCorrespondence current)
    {
        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Ces correspondances sont-elles correctes ?")
            .PageSize(PageSize)
            .AddChoices(Accept);

        foreach (CorrespondenceEntry entry in current.Entries)
        {
            _ = prompt.AddChoice("Corriger : " + entry.Operation);
        }

        return _console.Prompt(prompt.AddChoices(Decline));
    }

    /// <summary>
    /// Corrige une ligne : désigne une route, ou la déclare absente.
    /// </summary>
    /// <remarks>
    /// Une correction est marquée « déclarée », et non « confirmée ». Le développeur vient de poser cet
    /// appariement de sa main : le rapport doit pouvoir dire qu'il ne vient pas de l'outil, exactement
    /// comme s'il l'avait écrit dans un fichier.
    /// </remarks>
    private OperationCorrespondence Correct(
        OperationCorrespondence current, string choice, IReadOnlyList<string> routes)
    {
        const string Nothing = "Aucun appel correspondant";

        string operation = choice["Corriger : ".Length..];

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("À quelle route « " + Markup.Escape(operation) + " » correspond-elle ?")
                .PageSize(PageSize)
                .AddChoices(routes)
                .AddChoices(Nothing));

        bool absent = string.Equals(chosen, Nothing, StringComparison.Ordinal);

        CorrespondenceEntry Settled(CorrespondenceEntry entry)
        {
            if (!string.Equals(entry.Operation, operation, StringComparison.Ordinal))
            {
                return entry;
            }

            return entry with
            {
                Route = absent ? string.Empty : chosen,
                Origin = absent ? CorrespondenceOrigin.Ambiguous : CorrespondenceOrigin.Declared,
                Basis = "choisie par vous",
                Alternatives = [],
            };
        }

        return current with { Entries = [.. current.Entries.Select(Settled)] };
    }
}
