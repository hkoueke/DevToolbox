using System.Globalization;
using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Operations;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>
/// Écrit la production consolidée : les octets de la production, plus ce que le développeur a retenu.
/// </summary>
/// <remarks>
/// Une différence de conception avec le correctif que cet outil produisait auparavant, et elle vaut d'être
/// dite. Un fragment ne pouvait pas porter un objet incomplet — un schéma amputé de ses autres propriétés
/// ne se fusionne pas —, si bien qu'une différence sur un champ obligeait à emporter l'opération entière ;
/// c'est la règle que <see cref="Dependencies.CarriedObject"/> énonce. Une épissure, elle, remplace
/// exactement l'emplacement visé. Retenir une seule différence sur une route que la production porte déjà
/// change donc ce champ et rien d'autre, ce qu'un fragment ne savait pas faire.
/// <para>
/// <see cref="Dependencies.CarriedObject"/> reste employé, mais pour ce à quoi il sert vraiment ici :
/// établir de quels composants le contenu retenu a besoin.
/// </para>
/// <para>
/// Un candidat projeté depuis un contrat de service change deux choses, et deux seulement. Le contenu
/// retenu est <em>dérivé</em> et non recopié, si bien que sa trace nomme le document du contrat et ce que
/// la projection valait ; et là où la projection n'a rien porté, un marqueur le dit au lieu qu'un schéma
/// plausible le cache. Tout le reste — la sélection, l'énoncé, l'épissage, la garantie de l'octet
/// intact — est celui de la tranche précédente, et n'est pas réimplémenté.
/// </para>
/// </remarks>
public sealed class CorrectionBuilder : ICorrectionBuilder
{
    private const string Paths = "paths";
    private const string Components = "components";
    private const string Info = "info";
    private const string Version = "version";

    private readonly IOperationCatalog _catalog;
    private readonly ISplicingEmitter _splicer;

    /// <summary>Crée le constructeur.</summary>
    /// <param name="catalog">Énumère les routes, pour savoir ce que la production porte déjà.</param>
    /// <param name="splicer">Réécrit la production en ne touchant que les endroits désignés.</param>
    public CorrectionBuilder(IOperationCatalog catalog, ISplicingEmitter splicer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(splicer);

        _catalog = catalog;
        _splicer = splicer;
    }

    /// <inheritdoc />
    public Result<CorrectionPlan> Plan(CorrectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Preparation> prepared = Prepare(request);

        return prepared.IsFailure
            ? Result.Fail<CorrectionPlan>(prepared.Failure!)
            : Result.Success(prepared.Value.Plan);
    }

    /// <inheritdoc />
    public Result<BuiltCorrection> Build(CorrectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Preparation> prepared = Prepare(request);

        if (prepared.IsFailure)
        {
            return Result.Fail<BuiltCorrection>(prepared.Failure!);
        }

        Preparation preparation = prepared.Value;

        if (preparation.Plan.IsEmpty)
        {
            return Result.Fail<BuiltCorrection>(
                FailureReason.InvalidInput,
                "Rien n'a été retenu : il n'y a donc pas de production consolidée à écrire. Une sélection "
                    + "vide est une demande de ne rien écrire, jamais une demande de tout écrire.");
        }

        Result<ReadOnlyMemory<byte>> spliced = _splicer.Splice(
            request.Production, preparation.Splices, request.Formatting);

        return spliced.IsFailure
            ? Result.Fail<BuiltCorrection>(spliced.Failure!)
            : Result.Success(new BuiltCorrection(
                spliced.Value, preparation.Plan, preparation.Marks));
    }

    /// <summary>Établit les épissures, le plan et les traces, sans rien écrire.</summary>
    private Result<Preparation> Prepare(CorrectionRequest request)
    {
        HashSet<OperationSelector> held =
        [
            .. _catalog.List(request.Production).Select(operation => operation.Selector),
        ];

        Ledger ledger = new();

        foreach (OperationSelector route in request.Selection.Routes)
        {
            Route(request, route, held, ledger);
        }

        Changes(request, ledger);

        if (ledger.Routes.Count > 0 || ledger.Planned.Count > 0)
        {
            Required(request, ledger);
            Stamp(request, ledger);
        }

        CorrectionPlan plan = new(
            ledger.Routes,
            ledger.Planned,
            request.Closure.Required.Count,
            request.Version,
            ledger.Declined,
            ledger.Unfilled,
            ledger.Collisions);

        return Result.Success(new Preparation(ledger.Splices, plan, ledger.Marks));
    }

    /// <summary>
    /// Porte les composants dont le contenu retenu a besoin et que la production n'a pas.
    /// </summary>
    /// <remarks>
    /// Sans eux, une route reportée référencerait un schéma qui n'existe pas : le document se relirait,
    /// et la première validation de références le refuserait. C'est la panne que le correctif d'avant
    /// évitait déjà par la même fermeture, et il n'y avait aucune raison de l'oublier ici.
    /// </remarks>
    private static void Required(CorrectionRequest request, Ledger ledger)
    {
        foreach (SpecPointer component in request.Closure.Required)
        {
            if (PointerNavigator.Find(request.Production.Root, component.Json) is not null)
            {
                continue;
            }

            if (PointerNavigator.Find(request.Candidate.Root, component.Json) is not { } source)
            {
                ledger.Declined.Add(new DeclinedCorrection(
                    component.Display,
                    "Ce composant est nécessaire à ce qui a été retenu et ne se trouve dans aucune des "
                        + "deux descriptions : l'outil ne l'inventera pas."));

                continue;
            }

            IReadOnlyList<string> segments = component.Segments;

            if (segments.Count < 3)
            {
                continue;
            }

            SpecPointer family = SpecPointer.Root.Child(Components).Child(segments[1]);

            if (PointerNavigator.Find(request.Production.Root, family.Json) is null)
            {
                ledger.Declined.Add(new DeclinedCorrection(
                    component.Display,
                    "La production ne porte pas de section « " + segments[1] + " » sous « components » : "
                        + "l'outil ne crée pas la section, parce qu'il ne saurait pas où la poser sans "
                        + "réécrire ce qui l'entoure."));

                continue;
            }

            Component(request, ledger, component, family, source);
        }
    }

    /// <summary>Porte un composant, avec sa trace et ce que la projection n'a pas pu en porter.</summary>
    private static void Component(
        CorrectionRequest request,
        Ledger ledger,
        SpecPointer component,
        SpecPointer family,
        SpecNode source)
    {
        string name = component.Segments[2];
        DerivedOrigin? origin = request.Footing?.Derivation.ForComponent(name);
        IReadOnlyList<ProjectionGap> gaps =
            request.Footing?.Derivation.UnfilledAtComponent(name) ?? [];

        // Le composant entre tel quel. Il portait une trace « x-apidelta-origin » ; elle est retirée du
        // document, et son contenu se lit dans le rapport, à côté. Un fichier destiné à devenir un
        // contrat publié ne porte que de l'OpenAPI.
        ledger.Splices.Add(new Splice(SpliceKind.Add, family, name, source));

        Noted(ledger, component, gaps);
        ledger.Marks.Add(new CorrectionMark(component, component.Json, Derived: origin));
    }

    /// <summary>Inscrit la version recommandée.</summary>
    /// <remarks>
    /// C'est désormais la seule chose que cette méthode ajoute, et c'est la plus importante : le numéro
    /// de version est ce qui distingue le contrat consolidé de celui qui est en service, et donc ce qui
    /// fait qu'un changement incompatible n'atteint aucun appelant existant.
    /// <para>
    /// Le document portait aussi un bloc <c>x-apidelta-correction</c> — fichier source, empreinte,
    /// statut, nombre d'endroits non remplis, champs dérivés. Il a été retiré : ce qu'il disait se lit
    /// dans le rapport écrit à côté, et un fichier destiné à devenir un contrat publié ne porte que de
    /// l'OpenAPI. Le registre, lui, reste tenu — c'est lui qui alimente le rapport.
    /// </para>
    /// </remarks>
    private static void Stamp(CorrectionRequest request, Ledger ledger)
    {
        SpecPointer version = SpecPointer.Root.Child(Info).Child(Version);

        if (request.Version is { } recommended
            && recommended.Effective is { } next
            && PointerNavigator.Find(request.Production.Root, version.Json) is not null)
        {
            ledger.Splices.Add(new Splice(
                SpliceKind.Replace, version, Value: TreeEdits.Key(next.ToString())));
        }
    }


    /// <summary>Porte une route entière, quand la production ne l'a pas.</summary>
    private static void Route(
        CorrectionRequest request,
        OperationSelector route,
        HashSet<OperationSelector> held,
        Ledger ledger)
    {
        if (held.Contains(route))
        {
            ledger.Declined.Add(new DeclinedCorrection(
                route.Display,
                "La production porte déjà cette route. Pour en reprendre un changement, retenez la "
                    + "différence et non la route entière : l'écrire en bloc remplacerait ce que la "
                    + "production dit par ce que le candidat dit, y compris là où personne ne l'a demandé."));

            return;
        }

        SpecPointer operation = SpecPointer.Root.Child(Paths).Child(route.Path).Child(route.MethodKey);

        if (PointerNavigator.Find(request.Candidate.Root, operation.Json) is not { } source)
        {
            ledger.Declined.Add(new DeclinedCorrection(
                route.Display, "Le candidat ne porte pas cette route : il n'y a rien à en reprendre."));

            return;
        }

        SpecPointer item = SpecPointer.Root.Child(Paths).Child(route.Path);
        bool pathHeld = PointerNavigator.Find(request.Production.Root, item.Json) is not null;
        DerivedOrigin? origin = request.Footing?.Derivation.ForRoute(route.Method.ToString(), route.Path);
        IReadOnlyList<ProjectionGap> gaps =
            request.Footing?.Derivation.UnfilledAtRoute(route.Method.ToString(), route.Path, source)
            ?? [];

        SpecNode marked = source;

        // La méthode est ajoutée sous le chemin que la production porte, et jamais à la place : c'est la
        // forme de l'épissure qui le tient, et non une vérification qu'on pourrait oublier de faire.
        ledger.Splices.Add(pathHeld
            ? new Splice(SpliceKind.Add, item, route.MethodKey, marked)
            : new Splice(
                SpliceKind.Add,
                SpecPointer.Root.Child(Paths),
                route.Path,
                TreeEdits.Mapping(item, [new MappingEntry(TreeEdits.Key(route.MethodKey), marked)])));

        if (pathHeld && origin is { } derived)
        {
            ledger.Collisions.Add(new PathCollision(route.Path, route.Display, derived.Subject));
        }

        Noted(ledger, operation, gaps);
        ledger.Marks.Add(new CorrectionMark(operation, operation.Json, Derived: origin));
        ledger.Routes.Add(route);
    }

    /// <summary>Consigne ce qui restera non rempli à un emplacement, un marqueur par construction.</summary>
    private static void Noted(
        Ledger ledger, SpecPointer target, IReadOnlyList<ProjectionGap> gaps)
    {
        foreach (ProjectionGap gap in gaps)
        {
            ledger.Unfilled.Add(new UnfilledMarker(
                target, gap.Construct, gap.Subject, GapReasons.Say(gap.Reason)));
        }
    }

    /// <summary>Porte les différences retenues une par une.</summary>
    private static void Changes(CorrectionRequest request, Ledger ledger)
    {
        Dictionary<string, Difference> byLocation = Indexed(request.Differences);

        foreach (DifferenceSelector selector in request.Selection.Differences)
        {
            if (!byLocation.TryGetValue(Identity(selector), out Difference? difference))
            {
                ledger.Declined.Add(new DeclinedCorrection(
                    selector.Display,
                    "Cette différence n'est plus là : les descriptions ont été relues et la comparaison "
                        + "ne la trouve plus. Elle n'est pas reportée, parce que l'outil ne sait plus de "
                        + "quoi il parlerait."));

                continue;
            }

            Apply(request, selector, difference, ledger);
        }
    }

    /// <summary>Traduit une différence retenue en une épissure.</summary>
    private static void Apply(
        CorrectionRequest request,
        DifferenceSelector selector,
        Difference difference,
        Ledger ledger)
    {
        SpecPointer at = difference.Location;
        SpecNode? wanted = PointerNavigator.Find(request.Candidate.Root, at.Json);
        bool present = PointerNavigator.Find(request.Production.Root, at.Json) is not null;

        if (wanted is null)
        {
            if (!request.IncludeRemovals)
            {
                ledger.Declined.Add(new DeclinedCorrection(
                    selector.Display,
                    "Reporter cette différence supprimerait ce que la production dit ici, et les "
                        + "suppressions ne sont faites que si on les demande explicitement."));

                return;
            }

            ledger.Splices.Add(new Splice(SpliceKind.Remove, at));
        }
        else if (present)
        {
            ledger.Splices.Add(new Splice(SpliceKind.Replace, at, Value: wanted));
        }
        else
        {
            ledger.Declined.Add(new DeclinedCorrection(
                selector.Display,
                "La production ne porte rien à cet emplacement : l'y ajouter seul produirait un champ "
                    + "dont l'objet qui le contient n'existe pas. Retenez la route entière."));

            return;
        }

        ledger.Marks.Add(new CorrectionMark(
            at,
            at.Json,
            [at.Segments[^1]],
            difference.Before is { } before
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [at.Segments[^1]] = before.Text,
                }
                : null,
            request.Footing?.Derivation.For(at)));

        ledger.Planned.Add(new PlannedDifference(
            selector.Route,
            difference.ChangeId,
            at,
            difference.Impact,
            difference.Confidence,
            difference.Rationale));
    }

    /// <summary>Les différences, retrouvables par l'identité que porte une sélection.</summary>
    private static Dictionary<string, Difference> Indexed(IReadOnlyList<Difference> differences)
    {
        Dictionary<string, Difference> indexed = new(StringComparer.Ordinal);

        foreach (Difference difference in differences)
        {
            indexed[difference.ChangeId + "@" + difference.Location.Json] = difference;
        }

        return indexed;
    }

    private static string Identity(DifferenceSelector selector) =>
        selector.ChangeId + "@" + selector.At;

    /// <summary>Ce que l'établissement a produit, avant toute écriture.</summary>
    private sealed record Preparation(
        List<Splice> Splices, CorrectionPlan Plan, List<CorrectionMark> Marks);

    /// <summary>
    /// Ce que l'établissement accumule, le temps de l'établir.
    /// </summary>
    /// <remarks>
    /// Sept listes qui voyagent ensemble d'un bout à l'autre de l'établissement. Les passer une par une
    /// faisait des méthodes à huit paramètres dont l'ordre était la seule garantie : un registre les
    /// nomme, et un appel ne peut plus les échanger par distraction.
    /// </remarks>
    private sealed class Ledger
    {
        public List<Splice> Splices { get; } = [];

        public List<CorrectionMark> Marks { get; } = [];

        public List<DeclinedCorrection> Declined { get; } = [];

        public List<OperationSelector> Routes { get; } = [];

        public List<PlannedDifference> Planned { get; } = [];

        public List<UnfilledMarker> Unfilled { get; } = [];

        public List<PathCollision> Collisions { get; } = [];
    }
}
