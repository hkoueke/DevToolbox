namespace DevToolbox.Tools.ApiDelta.Infrastructure.Http;

/// <summary>Ce qu'une récupération a rapporté.</summary>
/// <param name="Origin">L'adresse finalement atteinte, après les redirections.</param>
/// <param name="Bytes">Les octets, tels qu'ils sont arrivés.</param>
/// <param name="Chain">Les redirections suivies, caviardées.</param>
internal sealed record RetrievedContent(
    Uri Origin, ReadOnlyMemory<byte> Bytes, IReadOnlyList<string> Chain);
