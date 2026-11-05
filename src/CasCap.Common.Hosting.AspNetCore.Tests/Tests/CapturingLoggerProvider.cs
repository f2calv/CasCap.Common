namespace CasCap.Common.Hosting.AspNetCore.Tests;

/// <summary>
/// Records what a forwarded Microsoft logging provider, such as the OpenTelemetry exporter with scopes enabled, receives.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<string> _records = new();
    private IExternalScopeProvider? _scopeProvider;

    /// <summary>Each forwarded record rendered with its state values and active scopes.</summary>
    public IReadOnlyCollection<string> Records => _records;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    /// <inheritdoc/>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    /// <inheritdoc/>
    public void Dispose() { }

    private void Capture<TState>(TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        using var writer = new StringWriter();
        writer.Write(formatter(state, exception));
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
            foreach (var (name, value) in values)
                writer.Write($" {name}={value}");
        _scopeProvider?.ForEachScope((scope, output) => output.Write($" scope={scope}"), writer);
        _records.Enqueue(writer.ToString());
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => provider._scopeProvider?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => provider.Capture(state, exception, formatter);
    }
}
