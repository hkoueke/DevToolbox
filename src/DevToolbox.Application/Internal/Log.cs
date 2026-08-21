using Microsoft.Extensions.Logging;

namespace DevToolbox.Application.Internal;

/// <summary>
/// Méthodes de journalisation générées à la source pour la couche applicative. Uniquement des propriétés
/// nommées et structurées : jamais de chaîne interpolée, jamais une valeur de variable.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Step {StepName} started.")]
    internal static partial void StepStarted(ILogger logger, string stepName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Step {StepName} completed in {ElapsedMilliseconds} ms.")]
    internal static partial void StepCompleted(ILogger logger, string stepName, long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Step {StepName} failed: {FailureReason}.")]
    internal static partial void StepFailed(ILogger logger, string stepName, string failureReason);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Run {ToolId} version {ToolVersion} started.")]
    internal static partial void RunStarted(ILogger logger, string toolId, string toolVersion);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Run {ToolId} ended as {Outcome} in {ElapsedMilliseconds} ms.")]
    internal static partial void RunEnded(
        ILogger logger, string toolId, string outcome, long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "Checkpoint written for run after step {StepName}.")]
    internal static partial void CheckpointWritten(ILogger logger, string stepName);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Recovery for failed step {StepName}: developer chose {Choice}.")]
    internal static partial void RecoveryChosen(ILogger logger, string stepName, string choice);
}
