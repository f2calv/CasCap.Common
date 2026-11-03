namespace CasCap.Common.Hosting.AspNetCore.Tests;

/// <summary>Collects every Serilog event written to the pipeline under test.</summary>
internal sealed class CapturingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    /// <summary>The events captured so far, in emission order.</summary>
    public IReadOnlyCollection<LogEvent> Events => _events;

    /// <inheritdoc/>
    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
