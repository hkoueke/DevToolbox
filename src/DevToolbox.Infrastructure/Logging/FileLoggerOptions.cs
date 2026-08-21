using Microsoft.Extensions.Logging;

namespace DevToolbox.Infrastructure.Logging;

/// <summary>Réglages du puits de journalisation à rotation de fichiers.</summary>
public sealed class FileLoggerOptions
{
    /// <summary>La section de configuration à laquelle ce type se lie.</summary>
    public const string SectionName = "Logging:File";

    /// <summary>Combien de jours de fichiers de journal conserver.</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Le niveau le plus bas écrit dans le fichier.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}
