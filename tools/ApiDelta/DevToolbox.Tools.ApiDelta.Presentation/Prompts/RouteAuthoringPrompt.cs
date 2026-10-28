using System.Globalization;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Authoring;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using Spectre.Console;

namespace DevToolbox.Tools.ApiDelta.Presentation.Prompts;

/// <summary>Demande, pour chaque opération retenue, ce que la route doit être.</summary>
/// <remarks>
/// Trois questions par opération retenue : le chemin, le verbe, et où la route se place dans la
/// production. La suggestion de la dérivation est pré-remplie, et ce qu'elle vaut est dit — une
/// suggestion montrée sans sa valeur passe pour un fait, et un repli affiché comme un itinéraire fait
/// passer plus vite sur une proposition qu'il aurait fallu regarder.
/// <para>
/// La validation vient du cœur et n'est pas refaite ici. Une invite qui porterait ses propres règles
/// finirait par en avoir d'autres que celles du cœur, et le document écrit obéirait aux siennes.
/// </para>
/// <para>
/// Le placement des champs n'est pas encore demandé : il est pris au défaut que le verbe permet. C'est la
/// tranche suivante qui le demandera, et la séparation est volontaire — une route posée au bon chemin
/// vaut déjà d'être livrée.
/// </para>
/// </remarks>
public sealed class RouteAuthoringPrompt : IRouteAuthoringPrompt
{
    private readonly IAnsiConsole _console;

    /// <summary>Crée l'invite.</summary>
    /// <param name="console">La console où demander.</param>
    public RouteAuthoringPrompt(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        _console = console;
    }

    /// <inheritdoc />
    public Task<AuthoringRecord> AuthorAsync(
        IReadOnlyList<AuthoringSubject> subjects,
        IReadOnlyList<string> productionPaths,
        IReadOnlyList<OfferedHeader> headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        ArgumentNullException.ThrowIfNull(productionPaths);
        ArgumentNullException.ThrowIfNull(headers);

        if (subjects.Count == 0)
        {
            return Task.FromResult(AuthoringRecord.Empty);
        }

        List<AuthoredRoute> authored = [];

        foreach (AuthoringSubject subject in Kept(subjects))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Rendre ce qui a été arrêté, et non le jeter : composer vingt et une opérations est une
                // longue séance, et une interruption ne doit pas coûter ce qui était décidé.
                break;
            }

            if (Compose(subject, productionPaths, authored) is { } route)
            {
                authored.Add(route);
            }
        }

        return Task.FromResult(new AuthoringRecord(authored));
    }

    /// <summary>Les opérations que le développeur retient.</summary>
    private IEnumerable<AuthoringSubject> Kept(IReadOnlyList<AuthoringSubject> subjects)
    {
        _console.MarkupLine(
            CultureInfo.InvariantCulture,
            "Le contrat porte [bold]{0}[/] opération(s).",
            subjects.Count.ToString(CultureInfo.InvariantCulture));

        IReadOnlyList<string> chosen =
        [
            .. _console.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Lesquelles voulez-vous ajouter ? [grey](espace pour cocher, entrée pour valider)[/]")
                    .NotRequired()
                    .PageSize(15)
                    .AddChoices(subjects.Select(subject => subject.Operation))),
        ];

        HashSet<string> kept = new(chosen, StringComparer.Ordinal);

        return subjects.Where(subject => kept.Contains(subject.Operation));
    }

    /// <summary>Compose une route : son chemin, son verbe, et où elle se place.</summary>
    private AuthoredRoute? Compose(
        AuthoringSubject subject,
        IReadOnlyList<string> productionPaths,
        IReadOnlyList<AuthoredRoute> already)
    {
        _console.MarkupLine(
            "[grey]Suggestion pour[/] [bold]" + Markup.Escape(subject.Operation) + "[/] : "
            + Markup.Escape(subject.Suggested.Display) + " [grey](" + Worth(subject) + ")[/]");

        string path = AskForPath(subject);
        HttpOperationMethod? method = AskForMethod(subject);

        if (method is null)
        {
            return null;
        }

        return new AuthoredRoute(
            subject.Operation,
            path,
            method.Value,
            PlacementDefaults.For(method.Value, subject.Fields),
            [],
            AskForPlace(productionPaths, already),
            subject.Worth,
            TookSuggestion: string.Equals(path, subject.Suggested.Path, StringComparison.Ordinal)
                && method.Value == subject.Suggested.Method);
    }

    /// <summary>Demande le chemin, en refusant ce que le cœur refuse.</summary>
    private string AskForPath(AuthoringSubject subject)
    {
        while (true)
        {
            string typed = _console.Prompt(
                new TextPrompt<string>("Quel chemin ?")
                    .DefaultValue(subject.Suggested.Path)
                    .ShowDefaultValue());

            Result<string> read = RouteSyntax.Path(typed);

            if (read.IsSuccess)
            {
                return read.Value;
            }

            _console.MarkupLine("[yellow]" + Markup.Escape(read.Failure!.Message) + "[/]");
        }
    }

    /// <summary>Demande le verbe, en refusant ce qu'OpenAPI ne définit pas.</summary>
    private HttpOperationMethod? AskForMethod(AuthoringSubject subject)
    {
        while (true)
        {
            string typed = _console.Prompt(
                new TextPrompt<string>("Quel verbe ?")
                    .DefaultValue(subject.Suggested.Method.ToString().ToUpperInvariant())
                    .ShowDefaultValue());

            Result<HttpOperationMethod> read = RouteSyntax.Method(typed);

            if (read.IsSuccess)
            {
                return read.Value;
            }

            _console.MarkupLine("[yellow]" + Markup.Escape(read.Failure!.Message) + "[/]");
        }
    }

    /// <summary>Demande où la route se place, dans l'ordre du document.</summary>
    /// <remarks>
    /// Dans l'ordre où la production écrit ses chemins, pour que le développeur choisisse un voisin qu'il
    /// lit. Les routes composées plus tôt dans la même séance s'ajoutent à la liste : une famille se
    /// compose souvent d'affilée.
    /// </remarks>
    private InsertionPoint AskForPlace(
        IReadOnlyList<string> productionPaths, IReadOnlyList<AuthoredRoute> already)
    {
        const string End = "à la fin";

        IReadOnlyList<string> choices =
        [
            .. productionPaths.Select(path => "après " + path),
            .. already.Select(route => "après " + route.Path),
            End,
        ];

        if (choices.Count == 1)
        {
            return InsertionPoint.AtEnd;
        }

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Où placer cette route dans votre production ?")
                .PageSize(15)
                .AddChoices(choices));

        return string.Equals(chosen, End, StringComparison.Ordinal)
            ? InsertionPoint.AtEnd
            : InsertionPoint.After(chosen["après ".Length..]);
    }

    /// <summary>Ce que vaut la suggestion, dans les mots du développeur.</summary>
    private static string Worth(AuthoringSubject subject) => subject.Worth switch
    {
        Core.Projection.RouteDerivationKind.Derived => "dérivée du nom de l'opération",
        Core.Projection.RouteDerivationKind.Partial => "partielle — aucun verbe reconnu",
        _ => "repli — rien de reconnaissable dans le nom",
    };
}
