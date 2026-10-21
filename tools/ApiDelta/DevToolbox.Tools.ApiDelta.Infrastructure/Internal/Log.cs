using Microsoft.Extensions.Logging;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Internal;

/// <summary>
/// Méthodes de journalisation générées à la source pour les adaptateurs d'apidelta.
/// </summary>
/// <remarks>
/// Uniquement des propriétés nommées et structurées, et uniquement des faits extérieurs au contenu : un
/// chemin, une taille, le début d'une empreinte, un verdict. Jamais une description, un exemple, une URL
/// de serveur ni un nœud — le contenu d'un document n'a rien à faire dans un journal, et les types qui le
/// portent sont par ailleurs enregistrés auprès du registre de masquage.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Probed {Path}: {ByteLength} bytes, sha256 {HashPrefix}.")]
    internal static partial void FileProbed(
        ILogger logger, string path, long byteLength, string hashPrefix);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Read {Path} as {Format} {Version} with {DiagnosticCount} diagnostics.")]
    internal static partial void ReadSucceeded(
        ILogger logger,
        string path,
        DevToolbox.Tools.ApiDelta.Core.Syntax.SpecFormat format,
        DevToolbox.Tools.ApiDelta.Core.Syntax.OpenApiVersion version,
        int diagnosticCount);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "Refused to read {Path}: {DiagnosticCount} blocking diagnostics.")]
    internal static partial void ReadRefused(ILogger logger, string path, int diagnosticCount);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "Wrote {Path}: {ByteLength} bytes, sha256 {HashPrefix}.")]
    internal static partial void ArtifactWritten(
        ILogger logger, string path, long byteLength, string hashPrefix);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "Refused to write {Path}: {Reason}.")]
    internal static partial void ArtifactRefused(ILogger logger, string path, string reason);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Refused {Path}: {Reason}.")]
    internal static partial void FileRefused(ILogger logger, string path, string reason);
}
