using DevToolbox.Application.Abstractions;

namespace DevToolbox.Infrastructure.Platform;

/// <summary>
/// L'horloge réelle. Le seul endroit où l'heure courante est lue depuis la machine.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
