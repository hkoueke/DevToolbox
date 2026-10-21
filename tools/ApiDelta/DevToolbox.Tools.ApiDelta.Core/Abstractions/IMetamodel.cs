using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Dit ce qu'un emplacement est, dans la grammaire d'OpenAPI.</summary>
public interface IMetamodel
{
    /// <summary>Résout un emplacement.</summary>
    /// <param name="location">L'emplacement, tel que l'arbre le nomme.</param>
    /// <param name="version">La version déclarée par le document.</param>
    /// <returns>
    /// Ce que la table en sait, ou son défaut sûr : un emplacement non décrit rend l'outil moins décidé,
    /// jamais faux.
    /// </returns>
    MetamodelEntry Resolve(SpecPointer location, OpenApiVersion version);
}
