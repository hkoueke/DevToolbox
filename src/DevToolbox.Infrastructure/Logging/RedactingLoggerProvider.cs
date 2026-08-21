using Microsoft.Extensions.Logging;

namespace DevToolbox.Infrastructure.Logging;

/// <summary>
/// Enveloppe un fournisseur de journalisation et impose le masquage à la frontière du puits.
/// </summary>
/// <remarks>
/// <para>
/// L'application est déjà écrite de façon qu'aucune valeur de variable ne soit jamais passée à un journal.
/// Ce décorateur existe parce que le masquage doit être imposé à la frontière plutôt que confié à la bonne
/// volonté des appelants : c'est le garde-fou qui tient encore lorsque quelqu'un, plus tard, écrit la
/// mauvaise ligne de journalisation.
/// </para>
/// <para>
/// Un argument interdit est remplacé avant la mise en forme, et la substitution est journalisée comme un
/// défaut : l'erreur se manifeste bruyamment au lieu d'être avalée en silence.
/// </para>
/// </remarks>
public sealed class RedactingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider _inner;
    private readonly RedactionRegistry _registry;

    /// <summary>Crée le décorateur.</summary>
    /// <param name="inner">Le fournisseur dont les journaux sont enveloppés.</param>
    /// <param name="registry">L'ensemble des types qui ne doivent jamais atteindre un puits.</param>
    public RedactingLoggerProvider(ILoggerProvider inner, RedactionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(registry);

        _inner = inner;
        _registry = registry;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        new RedactingLogger(_inner.CreateLogger(categoryName), _registry);

    /// <inheritdoc />
    public void Dispose() => _inner.Dispose();

    private sealed class RedactingLogger : ILogger
    {
        private readonly ILogger _inner;
        private readonly RedactionRegistry _registry;

        internal RedactingLogger(ILogger inner, RedactionRegistry registry)
        {
            _inner = inner;
            _registry = registry;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (state is IReadOnlyList<KeyValuePair<string, object?>> properties
                && TryRedact(properties, out List<KeyValuePair<string, object?>>? redacted))
            {
                // Signaler le défaut, puis écrire la forme expurgée pour que la trace reste continue.
                Internal.Log.RedactedAtSink(_inner, DescribeOffendingTypes(properties));

                _inner.Log(
                    logLevel,
                    eventId,
                    redacted!,
                    exception,
                    static (s, _) => FormatRedacted(s));

                return;
            }

            _inner.Log(logLevel, eventId, state, exception, formatter);
        }

        private static string FormatRedacted(IReadOnlyList<KeyValuePair<string, object?>> properties) =>
            string.Join(
                ", ",
                properties.Select(property => $"{property.Key}={property.Value}"));

        private string DescribeOffendingTypes(
            IReadOnlyList<KeyValuePair<string, object?>> properties) =>
            string.Join(
                ", ",
                properties
                    .Where(property => property.Value is not null
                        && _registry.IsForbidden(property.Value.GetType()))
                    .Select(property => property.Value!.GetType().Name)
                    .Distinct(StringComparer.Ordinal));

        private bool TryRedact(
            IReadOnlyList<KeyValuePair<string, object?>> properties,
            out List<KeyValuePair<string, object?>>? redacted)
        {
            redacted = null;

            foreach (KeyValuePair<string, object?> property in properties)
            {
                if (property.Value is null || !_registry.IsForbidden(property.Value.GetType()))
                {
                    continue;
                }

                redacted ??= [.. properties];

                int index = redacted.FindIndex(
                    candidate => string.Equals(candidate.Key, property.Key, StringComparison.Ordinal));

                if (index >= 0)
                {
                    redacted[index] = new KeyValuePair<string, object?>(
                        property.Key,
                        $"[redacted:{property.Value.GetType().Name}]");
                }
            }

            return redacted is not null;
        }
    }
}
