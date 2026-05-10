namespace Microsoft.Extensions.Logging;

/// <summary><see cref="ILoggerProvider"/> that routes log output to xUnit's <see cref="ITestOutputHelper"/>.</summary>
[ExcludeFromCodeCoverage]
public class TestLogProvider(ITestOutputHelper testOutputHelper) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, TestLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, _ => new TestLogger(testOutputHelper));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases resources used by this provider.</summary>
    protected virtual void Dispose(bool disposing)
    {
        // Cleanup
        _loggers.Clear();
    }
}
