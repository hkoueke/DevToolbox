using DevToolbox.Infrastructure.Logging;
using DevToolbox.Tools.VarCompare.Core.Abstractions;
using DevToolbox.Tools.VarCompare.Core.Groups;
using Microsoft.Extensions.DependencyInjection;

namespace DevToolbox.Tools.VarCompare.Infrastructure;

/// <summary>Enregistre les adaptateurs de varcompare : la passerelle en lecture seule et l'inspecteur de dossier.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Enregistre les implémentations d'infrastructure de varcompare.</summary>
    /// <param name="services">La collection de services.</param>
    /// <returns>La collection de services, pour le chaînage.</returns>
    public static IServiceCollection AddVarCompareInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IVariableGroupGateway, VariableGroupGateway>();
        services.AddSingleton<IWorkingFolderInspector, WorkingFolderInspector>();

        return services;
    }

    /// <summary>
    /// Déclare les types qui ne doivent jamais atteindre un puits de journalisation.
    /// </summary>
    /// <param name="registry">Le registre de masquage du puits de journalisation.</param>
    /// <remarks>
    /// Aujourd'hui, aucun code ne les passe à un journal. Les enregistrer malgré tout est précisément
    /// l'intérêt : le masquage au niveau du puits est le garde-fou qui tient encore lorsque quelqu'un écrit
    /// plus tard la mauvaise ligne de journalisation.
    /// </remarks>
    public static void RegisterRedactedTypes(RedactionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Forbid<VariableEntry>();
        registry.Forbid<VariableGroupSnapshot>();
    }
}
