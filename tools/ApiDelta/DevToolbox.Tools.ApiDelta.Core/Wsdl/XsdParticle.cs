namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un élément d'un contenu complexe, avec ses bornes d'occurrence.</summary>
/// <param name="Name">Son nom local.</param>
/// <param name="TypeName">Le nom qualifié de son type.</param>
/// <param name="MinOccurs">Le nombre minimal d'occurrences.</param>
/// <param name="MaxOccurs">Le nombre maximal, ou <see langword="null"/> pour <c>unbounded</c>.</param>
/// <param name="Nillable">L'élément accepte la valeur nulle.</param>
/// <param name="Location">Où il est déclaré.</param>
/// <remarks>
/// <c>minOccurs</c> vaut un par défaut en XSD, ce qui rend un élément ajouté à une séquence obligatoire
/// par construction. C'est le piège de versionnage le plus courant de SOAP, et la raison pour laquelle
/// cette borne est portée explicitement plutôt que déduite à l'usage.
/// </remarks>
public sealed record XsdParticle(
    string Name,
    string TypeName,
    int MinOccurs,
    int? MaxOccurs,
    bool Nillable,
    WsdlLocation Location)
{
    /// <summary>Indique que l'élément peut paraître plusieurs fois.</summary>
    public bool IsCollection => MaxOccurs is null || MaxOccurs > 1;

    /// <summary>Indique que l'élément est exigé.</summary>
    public bool IsRequired => MinOccurs > 0;
}
