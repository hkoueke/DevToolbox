namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Documents XML collectés sous contrôle réseau, avant interprétation du contrat.</summary>
/// <param name="Entry">Le document d'entrée matérialisé.</param>
/// <param name="Documents">Les documents originaux avec empreintes.</param>
/// <param name="ConfinementRoot">Le dossier de collecte.</param>
/// <param name="Retrieval">Correspondance d'URL en mémoire, jamais publiée dans un rapport.</param>
public sealed record CollectedWsdlGraph(MaterialisedDocument Entry, IReadOnlyList<MaterialisedDocument> Documents,
    string ConfinementRoot, RetrievalMap Retrieval);
