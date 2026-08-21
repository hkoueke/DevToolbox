using Microsoft.Extensions.Logging;

namespace DevToolbox.Tools.VarCompare.Infrastructure.Internal;

/// <summary>
/// Méthodes de journalisation générées à la source pour varcompare.
/// </summary>
/// <remarks>
/// Chaque lecture de groupe est consignée avec la collection, le projet, le nom et l'id du groupe, et
/// l'issue. Noter ce qu'aucun modèle de message n'accepte ici : une valeur de variable. Aucun paramètre ne
/// pourrait en porter une, ce qui constitue la première des deux lignes de défense, la seconde étant le
/// masquage appliqué au puits de journalisation.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "varcompare {ToolVersion} started against {Collection}.")]
    internal static partial void RunStarted(ILogger logger, string toolVersion, string collection);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Read group {GroupName} (#{GroupId}) in {Collection}/{Project}: {Outcome}.")]
    internal static partial void GroupRetrieved(
        ILogger logger,
        string groupName,
        int groupId,
        string collection,
        string project,
        string outcome);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Could not read group {GroupName} (#{GroupId}) in {Collection}/{Project}: {Reason}.")]
    internal static partial void GroupRetrievalFailed(
        ILogger logger,
        string groupName,
        int groupId,
        string collection,
        string project,
        string reason);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Comparison built over {VariableCount} variables across {GroupCount} groups.")]
    internal static partial void ComparisonBuilt(ILogger logger, int variableCount, int groupCount);
}
