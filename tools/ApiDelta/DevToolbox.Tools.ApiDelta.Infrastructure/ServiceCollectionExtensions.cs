using DevToolbox.Infrastructure.Logging;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Infrastructure.Emitting;
using DevToolbox.Tools.ApiDelta.Infrastructure.Files;
using DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;
using DevToolbox.Tools.ApiDelta.Infrastructure.Http;
using DevToolbox.Tools.ApiDelta.Infrastructure.Wsdl;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolbox.Tools.ApiDelta.Infrastructure;

/// <summary>
/// Enregistre les adaptateurs d'apidelta : lecture de fichiers, analyse, validation, émission.
/// </summary>
/// <remarks>
/// C'est le seul assemblage de l'outil qui connaisse YamlDotNet et Microsoft.OpenApi. Le cœur ne les voit
/// pas, et un test d'architecture le vérifie.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>Enregistre les implémentations d'infrastructure d'apidelta.</summary>
    /// <param name="services">La collection de services.</param>
    /// <returns>La collection de services, pour le chaînage.</returns>
    public static IServiceCollection AddApiDeltaInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISpecFileProbe, SpecFileProbe>();
        services.AddSingleton<ISpecReader, SpecReader>();
        services.AddSingleton<ISpecValidator, OpenApiSemanticAdapter>();
        services.AddSingleton<IReferenceGraph, ReferenceGraph>();
        services.AddSingleton<IVerbatimEmitter, VerbatimEmitter>();
        services.AddSingleton<IArtifactStore, ArtifactStore>();
        services.AddSingleton<ISpecEmitter, CanonicalEmitter>();
        services.AddSingleton<ISplicingEmitter, SplicingEmitter>();
        services.AddSingleton<IInPlaceWriter, InPlaceWriter>();
        services.AddSingleton<IRulesetStore, RulesetStore>();
        services.AddSingleton<ISuppressionStore, SuppressionStore>();

        services.AddSingleton<IWsdlReader, WsdlReader>();
        services.AddSingleton<AddressGuard>();

        HttpServices.Add(services);

        return services;
    }

    /// <summary>
    /// Déclare les types qui ne doivent jamais atteindre un puits de journalisation.
    /// </summary>
    /// <param name="registry">Le registre de masquage du puits de journalisation.</param>
    /// <remarks>
    /// Un nœud de description porte le contenu du document : une description, un exemple, une URL de
    /// serveur, et parfois un jeton que quelqu'un y a laissé. Les enregistrer ici est le garde-fou qui
    /// tient encore lorsque quelqu'un écrit plus tard la mauvaise ligne de journalisation.
    /// </remarks>
    public static void RegisterRedactedTypes(RedactionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Forbid<SpecNode>();
        registry.Forbid<MappingEntry>();
        registry.Forbid<ResolvedScalar>();

        // Un résumé de valeur existe pour atteindre un rapport, jamais un journal : il porte, borné, ce
        // que le document disait.
        registry.Forbid<DevToolbox.Tools.ApiDelta.Core.Comparison.ValueSummary>();

        // Un contrat de service porte ce qu'un WSDL déclare, y compris l'adresse de son service, et une
        // « soap:address » est l'endroit où un jeton se trouve quand il s'en trouve un. Le caviardage
        // est posé ici, au puits, et non à chaque appel : un garde-fou qu'il faut penser à invoquer
        // n'en est pas un, et la ligne de journalisation qui le contournera n'est pas encore écrite.
        registry.Forbid<Core.Wsdl.WsdlContract>();
        registry.Forbid<Core.Wsdl.WsdlDocument>();
        registry.Forbid<Core.Wsdl.WsdlService>();
        registry.Forbid<Core.Wsdl.WsdlPort>();

        // L'origine récupérée caviarde déjà son URL dans son constructeur — aucune instance ne peut en
        // porter une en clair. L'interdire ici tient tout de même : la redondance est voulue, parce que
        // les deux protections tomberaient pour des raisons différentes.
        registry.Forbid<Core.Wsdl.RetrievedOrigin>();

        // Un écart de projection nomme une construction et un emplacement du contrat. Ni l'un ni l'autre
        // n'a sa place dans un journal, et le rapport est le seul endroit qui les montre.
        registry.Forbid<Core.Projection.ProjectionGap>();
        registry.Forbid<Core.Projection.ProjectionResult>();
    }
}
