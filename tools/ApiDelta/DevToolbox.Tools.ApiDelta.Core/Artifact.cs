namespace DevToolbox.Tools.ApiDelta.Core;

/// <summary>Un fichier qu'une exécution a produit.</summary>
/// <param name="Kind">Sa sorte.</param>
/// <param name="Path">Où il a été écrit.</param>
/// <param name="ByteLength">Sa taille.</param>
/// <param name="Hash">Son empreinte, pour que le rapport puisse être recoupé plus tard.</param>
/// <remarks>
/// Un artefact n'est consigné qu'une fois le fichier durable. Une exécution annulée ou en échec n'en
/// consigne aucun pour l'étape qui n'a pas abouti : un rapport qui mentionnerait un fichier à moitié écrit
/// serait pire qu'un rapport muet.
/// </remarks>
public sealed record Artifact(ArtifactKind Kind, string Path, long ByteLength, string Hash);
