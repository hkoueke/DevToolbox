using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Une construction que la projection n'a pas portée, ou n'a portée qu'à peu près.</summary>
/// <param name="Reason">Pourquoi.</param>
/// <param name="Confidence">Ce que la projection valait : approchante, ou pas portée du tout.</param>
/// <param name="Construct">Ce dont il s'agit, tel que XSD l'écrit.</param>
/// <param name="Subject">Le type, l'opération ou la déclaration concernée.</param>
/// <param name="Location">Où la construction se trouve dans le contrat.</param>
/// <remarks>
/// Un écart <em>par occurrence</em>, et le groupement laissé au rapport. L'inverse — grouper ici —
/// perdrait l'emplacement de chaque occurrence, et un développeur à qui l'on annonce « quatre cents
/// attributs non portés » veut pouvoir aller voir le quatre-centième.
/// </remarks>
public sealed record ProjectionGap(
    ProjectionGapReason Reason,
    ProjectionConfidence Confidence,
    string Construct,
    string Subject,
    WsdlLocation Location);
