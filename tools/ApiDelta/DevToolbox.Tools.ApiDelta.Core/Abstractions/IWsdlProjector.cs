using DevToolbox.Tools.ApiDelta.Core.Projection;
using DevToolbox.Tools.ApiDelta.Core.Syntax;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Projette un contrat de service en document OpenAPI synthétique.</summary>
/// <remarks>
/// Le port existe pour que l'étape qui orchestre dépende de l'intention et non de l'implémentation, mais
/// aussi pour une raison plus concrète : un essai de l'étape doit pouvoir soumettre une projection
/// arbitraire — notamment une projection truffée d'écarts — sans construire un contrat qui les produise.
/// <para>
/// Pure par contrat : aucune implémentation ne lit l'horloge, le système de fichiers ni la console. La
/// même entrée doit donner le même document, sans quoi reprendre une séance en changerait le sens.
/// </para>
/// </remarks>
public interface IWsdlProjector
{
    /// <summary>Projette un contrat.</summary>
    /// <param name="contract">Le contrat lu.</param>
    /// <param name="version">La version qu'annonce la production.</param>
    /// <param name="verbs">La table de verbes, ou celle par défaut si rien n'est donné.</param>
    /// <returns>Le document synthétique, les écarts, les itinéraires et les chiffres.</returns>
    ProjectionResult Project(
        WsdlContract contract,
        OpenApiVersion version,
        IReadOnlyDictionary<string, string>? verbs = null);
}
