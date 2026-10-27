namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Un document du graphe : un <c>.wsdl</c> ou un <c>.xsd</c>.</summary>
/// <param name="Path">Où il est sur le disque — matérialisé depuis une URL ou lu tel quel.</param>
/// <param name="Kind">Ce qu'il est.</param>
/// <param name="ByteLength">Sa taille, telle que le budget la mesure.</param>
/// <param name="ContentHash">Son empreinte SHA-256, celle qu'une reprise confronte.</param>
/// <param name="TargetNamespace">Son espace de noms, absent pour une inclusion caméléon.</param>
/// <param name="Origin">D'où il vient, quand il a été récupéré plutôt que lu.</param>
/// <remarks>
/// Toujours un chemin, jamais une adresse : un document récupéré est écrit sur le disque avant d'être lu,
/// si bien que tout ce qui suit la lecture travaille sur des fichiers. C'est ce qui permet à la provenance,
/// aux rapports et à la reprise de n'avoir aucun cas particulier.
/// </remarks>
public sealed record WsdlDocument(
    string Path,
    WsdlDocumentKind Kind,
    long ByteLength,
    string ContentHash,
    string? TargetNamespace = null,
    RetrievedOrigin? Origin = null)
{
    /// <summary>Le nom du fichier, pour le dire à l'écran sans étaler un chemin.</summary>
    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>Indique que ce document a été récupéré plutôt que lu sur le disque.</summary>
    public bool WasRetrieved => Origin is not null;
}
