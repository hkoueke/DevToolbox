namespace DevToolbox.Application.Abstractions;

/// <summary>
/// Fournit l'heure courante. Injectée plutôt que lue depuis <c>DateTimeOffset.UtcNow</c>, afin que le
/// comportement du domaine et de la couche applicative soit reproductible sous test.
/// </summary>
public interface IClock
{
    /// <summary>L'heure courante, en UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
