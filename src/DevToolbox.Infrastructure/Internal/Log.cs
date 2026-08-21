using Microsoft.Extensions.Logging;

namespace DevToolbox.Infrastructure.Internal;

/// <summary>
/// Méthodes de journalisation générées à la source pour la couche d'infrastructure.
/// </summary>
/// <remarks>
/// Uniquement des propriétés nommées et structurées. Noter ce qui en est absent : aucun corps de réponse,
/// aucune URL complète avec sa chaîne de requête, et jamais une valeur de variable. Le code de statut est
/// consigné parce qu'il est diagnostique et ne porte aucun secret, mais il n'est volontairement jamais
/// remonté jusqu'à la console.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Warning,
        Message = "Azure DevOps request failed with status {StatusCode}.")]
    internal static partial void RequestFailed(ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Azure DevOps request could not reach the server.")]
    internal static partial void RequestTransportFailure(ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Checkpoint saved for run {RunId}.")]
    internal static partial void CheckpointSaved(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Swept {RemovedCount} expired run checkpoint(s).")]
    internal static partial void CheckpointsSwept(ILogger logger, int removedCount);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Error,
        Message = "A log argument of type {ArgumentType} was redacted at the sink boundary. This is a defect: "
            + "values must never be passed to a logger in the first place.")]
    internal static partial void RedactedAtSink(ILogger logger, string argumentType);
}
