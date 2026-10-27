using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>
/// Projette un contrat de service en document OpenAPI synthétique.
/// </summary>
/// <remarks>
/// C'est le pari central de cette tranche, et il est économique plutôt qu'élégant : plutôt que d'écrire
/// un second comparateur qui saurait lire du WSDL, on produit depuis le WSDL un document de la forme que
/// l'outil compare déjà. Le comparateur, le catalogue d'opérations, le constructeur de correction, le
/// mécanisme d'épissage et le vérificateur tournent alors sans être modifiés.
/// <para>
/// Ce que la projection laisse derrière est recensé, et non tu. Un document synthétique qui serait
/// présenté comme l'équivalent du contrat ferait prendre une décision de mise en production sur une
/// description dont un quart des contraintes a disparu.
/// </para>
/// <para>
/// Pur : pas d'horloge, pas de système de fichiers, pas de console. La même entrée donne le même
/// document, ce qui est ce dont un point de reprise a besoin.
/// </para>
/// </remarks>
public sealed class Projector : Abstractions.IWsdlProjector
{
    private const string Synthetic = "(projeté depuis le contrat de service)";

    /// <inheritdoc />
    public ProjectionResult Project(
        WsdlContract contract,
        OpenApiVersion version,
        IReadOnlyDictionary<string, string>? verbs = null)
    {
        ArgumentNullException.ThrowIfNull(contract);

        List<ProjectionGap> gaps = [];
        Dictionary<string, DerivedRoute> routes = new(StringComparer.Ordinal);

        SchemaDraft paths = new();
        SchemaDraft schemas = new();

        foreach (WsdlOperation operation in contract.Operations)
        {
            DerivedRoute route = RouteDerivation.Derive(operation.Name, verbs);

            routes[operation.Name] = route;

            Path(paths, contract, operation, route, version, gaps);
        }

        int declined = Types(contract, schemas, version, gaps);

        return new ProjectionResult(
            Document(paths, schemas, version),
            gaps,
            routes,
            Count(contract, gaps, declined));
    }

    /// <summary>Assemble le document : son en-tête, ses chemins, ses schémas.</summary>
    private static SpecDocument Document(
        SchemaDraft paths, SchemaDraft schemas, OpenApiVersion version)
    {
        SchemaDraft root = new();

        root.Set("openapi", version == OpenApiVersion.V31 ? "3.1.0" : "3.0.3");

        SchemaDraft info = new();

        info.Set("title", Synthetic);
        info.Set("version", "0.0.0");

        root.Set("info", info);
        root.Set("paths", paths);

        SchemaDraft components = new();

        components.Set("schemas", schemas);
        root.Set("components", components);

        return new SpecDocument(
            DocumentRole.Candidate,
            Synthetic,
            SpecFormat.Projected,
            version,
            0,
            string.Empty,
            DraftWriter.Write(root, SpecPointer.Root),
            ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>Pose un chemin pour une opération, avec sa charge utile.</summary>
    /// <remarks>
    /// La charge utile est le cœur de l'affaire. Une première version produisait des opérations sans
    /// corps de requête ni réponse : la comparaison y voyait bien les routes, mais ne pouvait jamais dire
    /// qu'un argument avait changé — c'est-à-dire la moitié de ce que l'outil promet.
    /// </remarks>
    private static void Path(
        SchemaDraft paths,
        WsdlContract contract,
        WsdlOperation operation,
        DerivedRoute route,
        OpenApiVersion version,
        List<ProjectionGap> gaps)
    {
        SchemaDraft item = paths.Section(route.Path);
        SchemaDraft action = new();

        action.Set("operationId", operation.Name);
        action.Set(
            "summary",
            operation.Documentation
                ?? ("Opération « " + operation.Name + " », schéma d'échange " + operation.Pattern));

        Body(action, contract, operation);
        Responses(action, contract, operation);

        Apply(
            new ProjectionSubject(
                operation.Name, operation.Location, Operation: operation, Version: version),
            action,
            gaps);

        if (operation.Binding is { } binding)
        {
            Apply(
                new ProjectionSubject(
                    operation.Name, operation.Location, Binding: binding, Version: version),
                action,
                gaps);
        }

        item.Set(route.Method.ToLowerInvariant(), action);
    }

    /// <summary>Pose le corps de la requête, si l'opération en reçoit un.</summary>
    private static void Body(SchemaDraft action, WsdlContract contract, WsdlOperation operation)
    {
        if (operation.Input is not { } input || Reference(contract, input) is not { } reference)
        {
            return;
        }

        SchemaDraft body = action.Section("requestBody");

        body.Set("required", true);
        body.Section("content").Section("application/json").Section("schema").Set("$ref", reference);
    }

    /// <summary>
    /// Pose la réponse de succès, si l'opération en rend une.
    /// </summary>
    /// <remarks>
    /// Le statut est 200 quand une réponse existe, 202 quand il n'y en a aucune. Les deux sont des
    /// conventions, pas des faits du contrat : un WSDL ne déclare aucun code de statut, et le rapport le
    /// dit — annoncer un code comme venant du contrat serait affirmer ce qu'on a choisi.
    /// </remarks>
    private static void Responses(
        SchemaDraft action, WsdlContract contract, WsdlOperation operation)
    {
        SchemaDraft responses = action.Section("responses");

        if (!operation.HasResponse)
        {
            SchemaDraft accepted = new();

            accepted.Set("description", "Aucune réponse : le contrat ne déclare pas de message de sortie.");
            responses.Set("202", accepted);

            return;
        }

        SchemaDraft success = new();

        success.Set("description", "Réponse de « " + operation.Name + " ». Statut déduit, non déclaré.");

        if (operation.Output is { } output && Reference(contract, output) is { } reference)
        {
            success.Section("content").Section("application/json").Section("schema")
                .Set("$ref", reference);
        }

        responses.Set("200", success);
    }

    /// <summary>
    /// La référence de schéma que porte le corps d'un message.
    /// </summary>
    /// <remarks>
    /// Une partie de message désigne presque toujours un <em>élément</em>, dont il faut passer par le
    /// type pour obtenir un schéma. Une partie désignant directement un type est licite en
    /// « rpc/literal » et se résout d'un cran de moins.
    /// </remarks>
    private static string? Reference(WsdlContract contract, WsdlMessage message)
    {
        WsdlPart? body = message.Parts.FirstOrDefault(part => part.IsWellFormed);

        if (body is null)
        {
            return null;
        }

        if (body.TypeName is { } named)
        {
            return Pointer(named);
        }

        if (body.ElementName is not { } element)
        {
            return null;
        }

        return contract.Elements.TryGetValue(Qualified(contract, element), out string? type)
            ? Pointer(type)
            : null;
    }

    /// <summary>
    /// Le nom qualifié d'un élément désigné par un préfixe.
    /// </summary>
    /// <remarks>
    /// Le préfixe du document n'est pas résolu ici : la table des éléments est consultée sur le nom
    /// local, parce que deux espaces de noms ne portent pratiquement jamais le même nom d'élément de
    /// message, et parce que résoudre un préfixe demanderait de reporter la table des espaces de noms du
    /// document jusqu'ici — une dépendance que la projection n'a pas besoin de prendre.
    /// </remarks>
    private static string Qualified(WsdlContract contract, string prefixed) =>
        contract.Elements.Keys.FirstOrDefault(known =>
            string.Equals(Local(known), Local(prefixed), StringComparison.Ordinal))
            ?? prefixed;

    private static string Pointer(string type) => "#/components/schemas/" + Local(type);

    /// <summary>Projette chaque type, et rend le nombre de ceux qui ont été déclinés.</summary>
    private static int Types(
        WsdlContract contract,
        SchemaDraft schemas,
        OpenApiVersion version,
        List<ProjectionGap> gaps)
    {
        HashSet<string> known = new(contract.Types.Keys, StringComparer.Ordinal);
        int declined = 0;

        foreach (KeyValuePair<string, XsdType> type in contract.Types)
        {
            if (type.Value.UsesXsd11)
            {
                // Décliné, et non projeté à peu près : un type dont le sens dépend d'une construction
                // que JSON Schema ne sait pas exprimer serait projeté plus permissif que le vrai.
                ProjectionSubject refused = new(
                    type.Key, type.Value.Location, Type: type.Value, Version: version);

                foreach (IProjectionRule rule in ProjectionTable.For(refused))
                {
                    rule.Apply(refused, new SchemaDraft(), gaps);
                }

                declined++;

                continue;
            }

            schemas.Set(Local(type.Key), Type(type.Value, version, known, gaps));
        }

        return declined;
    }

    /// <summary>Projette un type : ses règles, puis celles de ses parties.</summary>
    private static SchemaDraft Type(
        XsdType type,
        OpenApiVersion version,
        IReadOnlySet<string> known,
        List<ProjectionGap> gaps)
    {
        SchemaDraft draft = new();

        Apply(
            new ProjectionSubject(
                type.Name, type.Location, Type: type, Version: version, KnownTypes: known),
            draft,
            gaps);

        // Les particules d'un choix sont projetées par la règle du choix, dans ses branches. Les
        // projeter ici en plus poserait les mêmes propriétés au niveau du type et les déclarerait toutes
        // obligatoires — un document affirmant que les trois branches apparaissent ensemble, soit
        // exactement le contraire de ce que le contrat dit.
        foreach (XsdParticle particle in type.Content == XsdContentKind.Choice ? [] : type.Particles)
        {
            Apply(
                new ProjectionSubject(
                    type.Name,
                    particle.Location,
                    Particle: particle,
                    Version: version,
                    KnownTypes: known),
                draft,
                gaps);
        }

        foreach (XsdFacet facet in type.Facets)
        {
            Apply(
                new ProjectionSubject(type.Name, type.Location, Facet: facet, Version: version),
                draft,
                gaps);
        }

        foreach (XsdAttributeUse attribute in type.Attributes)
        {
            Apply(
                new ProjectionSubject(
                    type.Name, attribute.Location, Attribute: attribute, Version: version),
                draft,
                gaps);
        }

        return draft;
    }

    private static void Apply(
        ProjectionSubject subject, SchemaDraft draft, List<ProjectionGap> gaps)
    {
        foreach (IProjectionRule rule in ProjectionTable.For(subject))
        {
            rule.Apply(subject, draft, gaps);
        }
    }

    /// <summary>Compte ce que la projection a produit.</summary>
    private static ProjectionFigures Count(
        WsdlContract contract, List<ProjectionGap> gaps, int declined) =>
        new(
            contract.Operations.Count,
            contract.Types.Count - declined,
            Carried(contract) - gaps.Count,
            gaps.Count(gap => gap.Confidence == ProjectionConfidence.Approximate),
            gaps.Count(gap => gap.Confidence == ProjectionConfidence.NotCarried),
            declined);

    /// <summary>
    /// Le nombre de constructions que le contrat soumet à la projection.
    /// </summary>
    /// <remarks>
    /// Compté sur le contrat, et non sur ce qui est sorti. Un chiffre tiré du document produit ne
    /// pourrait pas dire ce qui n'y est pas, ce qui est précisément la question que ces chiffres servent
    /// à répondre.
    /// </remarks>
    private static int Carried(WsdlContract contract) =>
        contract.Types.Values.Sum(type =>
            1 + type.Particles.Count + type.Facets.Count + type.Attributes.Count)
        + contract.Operations.Count;

    private static string Local(string qualified)
    {
        int separator = qualified.LastIndexOf(':');

        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }
}
