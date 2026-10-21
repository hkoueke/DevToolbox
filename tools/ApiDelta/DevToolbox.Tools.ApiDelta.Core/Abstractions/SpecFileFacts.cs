namespace DevToolbox.Tools.ApiDelta.Core.Abstractions;

/// <summary>Ce qu'on sait d'un fichier de description avant de l'avoir analysé.</summary>
/// <param name="FullPath">Son chemin canonique.</param>
/// <param name="ByteLength">Sa taille.</param>
/// <param name="Sha256">Son empreinte, en hexadécimal minuscule.</param>
public sealed record SpecFileFacts(string FullPath, long ByteLength, string Sha256);
