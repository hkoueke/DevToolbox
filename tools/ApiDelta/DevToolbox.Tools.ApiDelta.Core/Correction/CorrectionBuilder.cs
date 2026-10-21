using DevToolbox.Domain.Results;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Comparison;
using DevToolbox.Tools.ApiDelta.Core.Extraction;
using DevToolbox.Tools.ApiDelta.Core.Optimization;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Correction;

/// <summary>
/// Écrit la production corrigée : les octets de la production, plus ce que le développeur a retenu.
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
/// </remarks>
public sealed class CorrectionBuilder : ICorrectionBuilder
{
    private const string Paths = "paths";
    private const string Components = "components";
    private const string Info = "info";
    private const string Version = "version";

    /// <summary>Le membre qui déclare, au niveau du document, qu'il s'agit d'une correction proposée.</summary>
    public const string Declaration = "x-apidelta-correction";

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
                "Rien n'a été retenu : il n'y a donc pas de production corrigée à écrire. Une sélection "
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

        List<Splice> splices = [];
        List<CorrectionMark> marks = [];
        List<DeclinedCorrection> declined = [];
        List<OperationSelector> routes = [];

        foreach (OperationSelector route in request.Selection.Routes)
        {
            Route(request, route, held, splices, marks, declined, routes);
        }

        List<PlannedDifference> planned = Changes(request, splices, marks, declined);

        if (routes.Count > 0 || planned.Count > 0)
        {
            Required(request, splices, marks, declined);
            Stamp(request, splices);
        }

        CorrectionPlan plan = new(
            routes, planned, request.Closure.Required.Count, request.Version, declined);

        return Result.Success(new Preparation(splices, plan, marks));
    }



    /// <summary>
    /// Porte les composants dont le contenu retenu a besoin et que la production n'a pas.
    /// </summary>
    /// <remarks>
    /// Sans eux, une route reportée référencerait un schéma qui n'existe pas : le document se relirait,
    /// et la première validation de références le refuserait. C'est la panne que le correctif d'avant
    /// évitait déjà par la même fermeture, et il n'y avait aucune raison de l'oublier ici.
    /// </remarks>
    private static void Required(
        CorrectionRequest request,
        List<Splice> splices,
        List<CorrectionMark> marks,
        List<DeclinedCorrection> declined)
    {
        foreach (SpecPointer component in request.Closure.Required)
        {
            if (PointerNavigator.Find(request.Production.Root, component.Json) is not null)
            {
                continue;
            }

            if (PointerNavigator.Find(request.Candidate.Root, component.Json) is not { } source)
            {
                declined.Add(new DeclinedCorrection(
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
                declined.Add(new DeclinedCorrection(
                    component.Display,
                    "La production ne porte pas de section « " + segments[1] + " » sous « components » : "
                        + "l'outil ne crée pas la section, parce qu'il ne saurait pas où la poser sans "
                        + "réécrire ce qui l'entoure."));

                continue;
            }

            splices.Add(new Splice(
                SpliceKind.Add, family, segments[2], Mark(source, component.Json)));

            marks.Add(new CorrectionMark(component, component.Json));
        }
    }
    /// <summary>Inscrit la version recommandée, et déclare que le document est une correction proposée.</summary>
    /// <remarks>
    /// La déclaration n'est pas une politesse : sans elle, une production corrigée est indistinguable
    /// d'une production publiée, et c'est le seul endroit où cet outil pourrait laisser croire qu'un
    /// document est un contrat alors qu'il est une proposition.
    /// </remarks>
    private static void Stamp(CorrectionRequest request, List<Splice> splices)
    {
        SpecPointer version = SpecPointer.Root.Child(Info).Child(Version);

        if (request.Version is { } recommended
            && recommended.Effective is { } next
            && PointerNavigator.Find(request.Production.Root, version.Json) is not null)
        {
            splices.Add(new Splice(
                SpliceKind.Replace, version, Value: TreeEdits.Key(next.ToString())));
        }

        SpecPointer root = SpecPointer.Root;

        splices.Add(new Splice(
            SpliceKind.Add,
            root,
            Declaration,
            TreeEdits.Mapping(
                root.Child(Declaration),
                [
                    // Le nom du fichier et son empreinte, jamais son chemin : un chemin absolu varie d'une
                    // machine à l'autre, ce qui rendrait deux exécutions identiques distinguables, et il
                    // porterait le dossier personnel du développeur dans un document destiné à être
                    // validé. L'empreinte dit l'identité, ce que le chemin ne disait pas.
                    new MappingEntry(
                        TreeEdits.Key("source"),
                        TreeEdits.Key(Path.GetFileName(request.Candidate.Path))),
                    new MappingEntry(
                        TreeEdits.Key("sourceHash"), TreeEdits.Key(request.Candidate.ContentHash)),
                    new MappingEntry(
                        TreeEdits.Key("statut"),
                        TreeEdits.Key("correction proposée, non publiée")),
                ])));
    }
    /// <summary>Porte une route entière, quand la production ne l'a pas.</summary>
    private static void Route(
        CorrectionRequest request,
        OperationSelector route,
        HashSet<OperationSelector> held,
        List<Splice> splices,
        List<CorrectionMark> marks,
        List<DeclinedCorrection> declined,
        List<OperationSelector> carried)
    {
        if (held.Contains(route))
        {
            declined.Add(new DeclinedCorrection(
                route.Display,
                "La production porte déjà cette route. Pour en reprendre un changement, retenez la "
                    + "différence et non la route entière : l'écrire en bloc remplacerait ce que la "
                    + "production dit par ce que le candidat dit, y compris là où personne ne l'a demandé."));

            return;
        }

        SpecPointer operation = SpecPointer.Root.Child(Paths).Child(route.Path).Child(route.MethodKey);

        if (PointerNavigator.Find(request.Candidate.Root, operation.Json) is not { } source)
        {
            declined.Add(new DeclinedCorrection(
                route.Display, "Le candidat ne porte pas cette route : il n'y a rien à en reprendre."));

            return;
        }

        SpecPointer item = SpecPointer.Root.Child(Paths).Child(route.Path);
        bool pathHeld = PointerNavigator.Find(request.Production.Root, item.Json) is not null;
        SpecNode marked = Mark(source, operation.Json);

        splices.Add(pathHeld
            ? new Splice(SpliceKind.Add, item, route.MethodKey, marked)
            : new Splice(
                SpliceKind.Add,
                SpecPointer.Root.Child(Paths),
                route.Path,
                TreeEdits.Mapping(item, [new MappingEntry(TreeEdits.Key(route.MethodKey), marked)])));

        marks.Add(new CorrectionMark(operation, operation.Json));
        carried.Add(route);
    }

    /// <summary>Porte les différences retenues une par une.</summary>
    private static List<PlannedDifference> Changes(
        CorrectionRequest request,
        List<Splice> splices,
        List<CorrectionMark> marks,
        List<DeclinedCorrection> declined)
    {
        List<PlannedDifference> planned = [];
        Dictionary<string, Difference> byLocation = Indexed(request.Differences);

        foreach (DifferenceSelector selector in request.Selection.Differences)
        {
            if (!byLocation.TryGetValue(Identity(selector), out Difference? difference))
            {
                declined.Add(new DeclinedCorrection(
                    selector.Display,
                    "Cette différence n'est plus là : les descriptions ont été relues et la comparaison "
                        + "ne la trouve plus. Elle n'est pas reportée, parce que l'outil ne sait plus de "
                        + "quoi il parlerait."));

                continue;
            }

            Apply(request, selector, difference, splices, marks, declined, planned);
        }

        return planned;
    }

    /// <summary>Traduit une différence retenue en une épissure.</summary>
    private static void Apply(
        CorrectionRequest request,
        DifferenceSelector selector,
        Difference difference,
        List<Splice> splices,
        List<CorrectionMark> marks,
        List<DeclinedCorrection> declined,
        List<PlannedDifference> planned)
    {
        SpecPointer at = difference.Location;
        SpecNode? wanted = PointerNavigator.Find(request.Candidate.Root, at.Json);
        bool present = PointerNavigator.Find(request.Production.Root, at.Json) is not null;

        if (wanted is null)
        {
            if (!request.IncludeRemovals)
            {
                declined.Add(new DeclinedCorrection(
                    selector.Display,
                    "Reporter cette différence supprimerait ce que la production dit ici, et les "
                        + "suppressions ne sont faites que si on les demande explicitement."));

                return;
            }

            splices.Add(new Splice(SpliceKind.Remove, at));
        }
        else if (present)
        {
            splices.Add(new Splice(SpliceKind.Replace, at, Value: wanted));
        }
        else
        {
            declined.Add(new DeclinedCorrection(
                selector.Display,
                "La production ne porte rien à cet emplacement : l'y ajouter seul produirait un champ "
                    + "dont l'objet qui le contient n'existe pas. Retenez la route entière."));

            return;
        }

        marks.Add(new CorrectionMark(
            at,
            at.Json,
            [at.Segments[^1]],
            difference.Before is { } before
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [at.Segments[^1]] = before.Text,
                }
                : null));

        planned.Add(new PlannedDifference(
            selector.Route,
            difference.ChangeId,
            at,
            difference.Impact,
            difference.Confidence,
            difference.Rationale));
    }

    /// <summary>Pose la trace d'origine sur un objet, quand il peut en porter une.</summary>
    /// <remarks>
    /// Sur un objet seulement : OpenAPI n'autorise les membres <c>x-</c> que là. Un champ scalaire voit sa
    /// trace figurer dans la déclaration du document, qui nomme le champ — le lecteur l'apprend donc sans
    /// ouvrir le rapport, ce qui est ce qui était promis.
    /// </remarks>
    private static SpecNode Mark(SpecNode node, string from)
    {
        if (node is not MappingNode mapping)
        {
            return node;
        }

        MappingNode trace = TreeEdits.Mapping(
            node.Location.Child(CorrectionMark.Member),
            [new MappingEntry(TreeEdits.Key("from"), TreeEdits.Key(from))]);

        return TreeEdits.Mapping(
            mapping.Location,
            [
                .. mapping.Entries,
                new MappingEntry(TreeEdits.Key(CorrectionMark.Member), trace),
            ]);
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
}
