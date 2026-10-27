namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un document récupéré puis écrit sur le disque.</summary>
/// <param name="Path">Où il a été écrit.</param>
/// <param name="Origin">L'adresse d'où il vient, caviardée, et l'instant de sa récupération.</param>
/// <param name="ByteLength">Sa taille.</param>
/// <param name="ContentHash">Son empreinte.</param>
/// <remarks>
/// L'origine est caviardée dès cette forme, et non à l'affichage : un identifiant vit dans la chaîne de
/// requête d'une adresse de service bien plus souvent qu'on ne le voudrait, et ce qui n'est jamais entré
/// dans la forme ne peut pas s'en échapper par un puits qu'on aurait oublié.
/// </remarks>
public sealed record MaterialisedDocument(
    string Path, RetrievedOrigin Origin, long ByteLength, string ContentHash);
