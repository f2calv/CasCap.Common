namespace Microsoft.Extensions.Logging;

/// <summary>Immutable log entry captured by <see cref="TestLogger"/>.</summary>
[ExcludeFromCodeCoverage]
class LogEntry(LogLevel level, string message)
{
    /// <summary>UTC timestamp when the entry was recorded.</summary>
    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>The severity level of the log entry.</summary>
    public LogLevel LogLevel { get; } = level;

    /// <summary>The formatted log message.</summary>
    public string Message { get; } = message;

    /// <inheritdoc/>
    public override string ToString() => $"{Timestamp:o}: {Message}";
}
