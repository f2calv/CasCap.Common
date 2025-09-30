namespace Microsoft.Extensions.Logging;

[ExcludeFromCodeCoverage]
public class TestLogProvider(ITestOutputHelper testOutputHelper) : ILoggerProvider
{
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
    private readonly ConcurrentDictionary<string, TestLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, _ => new TestLogger(_testOutputHelper));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // Cleanup
        _loggers.Clear();
    }
}

[ExcludeFromCodeCoverage]
class TestLogger(ITestOutputHelper output) : ILogger
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyCollection<LogEntry> GetLogs() => _entries.AsReadOnly();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var entry = new LogEntry(logLevel, formatter(state, exception));
        _entries.Add(entry);
        output.WriteLine(entry.ToString());
    }
}

[ExcludeFromCodeCoverage]
class LogEntry(LogLevel level, string message)
{
    public DateTime Timestamp { get; } = DateTime.Now;

    public LogLevel LogLevel { get; } = level;

    public string Message { get; } = message;

    public override string ToString() => $"{Timestamp:o}: {Message}";
}
