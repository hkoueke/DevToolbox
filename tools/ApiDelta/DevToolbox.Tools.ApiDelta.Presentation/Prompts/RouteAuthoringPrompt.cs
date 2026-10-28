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
            AskForPlacements(subject, method.Value, path),
            [],
            AskForPlace(productionPaths, already),
            subject.Worth,
            TookSuggestion: string.Equals(path, subject.Suggested.Path, StringComparison.Ordinal)
                && method.Value == subject.Suggested.Method);
    }

    /// <summary>Demande où va chaque champ, puis s'il est obligatoire.</summary>
    /// <remarks>
    /// Le défaut que le verbe permet est proposé d'abord, et montré : une opération de vingt champs
    /// serait inutilisable sans lui, et un défaut appliqué sans être montré cacherait une décision.
    /// <para>
    /// Les marqueurs du chemin sont proposés d'emblée dans le chemin, puisque le développeur vient de les
    /// écrire. C'est la seule déduction que l'invite se permet, et elle vient de sa propre réponse.
    /// </para>
    /// </remarks>
    private List<FieldPlacement> AskForPlacements(
        AuthoringSubject subject, HttpOperationMethod method, string path)
    {
        if (subject.HasNoFields)
        {
            return [];
        }

        HashSet<string> inPath = new(RouteSyntax.Placeholders(path), StringComparer.Ordinal);
        List<FieldPlacement> settled = [];

        foreach (FieldPlacement proposed in PlacementDefaults.For(method, subject.Fields))
        {
            ParameterLocation where = inPath.Contains(proposed.Field)
                ? ParameterLocation.Path
                : AskWhere(proposed, method);

            settled.Add(new FieldPlacement(
                proposed.Field,
                proposed.Declared,
                proposed.IsComposite,
                where,
                AskIfRequired(proposed.Field, where)));
        }

        return settled;
    }

    /// <summary>Demande la destination d'un champ, en n'offrant que les possibles.</summary>
    private ParameterLocation AskWhere(FieldPlacement proposed, HttpOperationMethod method)
    {
        IReadOnlyList<ParameterLocation> possible =
        [
            .. Enum.GetValues<ParameterLocation>()
                .Where(location => PlacementRules.Allows(proposed.IsComposite, location)),
        ];

        if (PlacementDefaults.AwaitsDecision(method, proposed))
        {
            _console.MarkupLine(
                "[yellow]« " + Markup.Escape(proposed.Field) + " » est de type "
                + Markup.Escape(proposed.Declared) + ", qui n'est pas un scalaire : sous ce verbe, "
                + "l'outil n'a pas de destination à proposer.[/]");
        }

        string chosen = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("Où va « " + Markup.Escape(proposed.Field) + " » ? [grey]("
                    + Markup.Escape(proposed.Declared) + ")[/]")
                .AddChoices(possible.Select(FieldPlacement.Where))
                .UseConverter(label => label));

        return possible.First(location =>
            string.Equals(FieldPlacement.Where(location), chosen, StringComparison.Ordinal));
    }

    /// <summary>Demande si un champ placé est obligatoire, sauf dans le chemin.</summary>
    /// <remarks>
    /// Le contrat ne le dit pas — tout y est optionnel et nillable — et la question le dit, pour qu'un
    /// développeur sache que ce n'est pas une information qu'on lui redemande mais une décision qu'on lui
    /// demande. Un paramètre de chemin ne pose pas la question : OpenAPI l'exige obligatoire.
    /// </remarks>
    private bool AskIfRequired(string field, ParameterLocation where)
    {
        if (where is ParameterLocation.Path or ParameterLocation.Nowhere)
        {
            return where is ParameterLocation.Path;
        }

        _console.MarkupLine(
            "[grey]Le contrat ne dit pas si « " + Markup.Escape(field)
            + " » est obligatoire : tout y est optionnel et nillable.[/]");

        return _console.Prompt(
            new ConfirmationPrompt("« " + Markup.Escape(field) + " » est-il obligatoire ?")
            {
                DefaultValue = false,
            });
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
