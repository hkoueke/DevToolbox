using Microsoft.Extensions.Logging;

namespace DevToolbox.Tests.Infrastructure;

/// <summary>
/// Capture tout ce qui lui est écrit, mis en forme exactement comme un puits le recevrait.
/// </summary>
/// <remarks>
/// Mettre en forme le message plutôt que de conserver l'objet d'état est délibéré : un secret qui
/// n'atteindrait le journal qu'à travers un modèle de message resterait invisible à un test qui
/// n'inspecterait que les arguments.
/// </remarks>
internal sealed class RecordingLogger : ILogger
{
    private readonly List<string> _lines = [];

    internal IReadOnlyList<string> Lines => _lines;

    /// <summary>Tout ce qui a été capturé, concaténé, pour une vérification d'absence en une seule fois.</summary>
    internal string AllText => string.Join(Environment.NewLine, _lines);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        _lines.Add($"scope: {state}");
        return null;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _lines.Add($"[{logLevel}] {eventId.Id} {formatter(state, exception)}");
    }
}
