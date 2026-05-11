namespace Microsoft.Extensions.Logging;

/// <summary><see cref="ILogger"/> implementation that captures log entries and writes them to xUnit's <see cref="ITestOutputHelper"/>.</summary>
[ExcludeFromCodeCoverage]
class TestLogger(ITestOutputHelper output) : ILogger
{
    private readonly List<LogEntry> _entries = [];

    /// <summary>Returns all log entries captured by this logger.</summary>
    public IReadOnlyCollection<LogEntry> GetLogs() => _entries.AsReadOnly();

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null!;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    /// <remarks>
    /// xUnit's <see cref="ITestOutputHelper"/> throws <see cref="InvalidOperationException"/>
    /// when no test is active (e.g. during async disposal).
    /// See <see href="https://github.com/xunit/xunit/issues/1540" />.
    /// </remarks>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var entry = new LogEntry(logLevel, formatter(state, exception));
        _entries.Add(entry);
        try
        {
            output.WriteLine(entry.ToString());
        }
        catch (InvalidOperationException)
        {
            // https://github.com/xunit/xunit/issues/1540
        }
    }
}
