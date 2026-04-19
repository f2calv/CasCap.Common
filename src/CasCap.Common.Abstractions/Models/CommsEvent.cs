#if NET8_0_OR_GREATER
namespace CasCap.Common.Abstractions;

/// <summary>
/// Represents a key event written to a Redis Stream for consumption by a
/// communications feature. An agent reads these events and decides whether
/// (and how) to relay them to a notification group.
/// </summary>
/// <remarks>
/// Only significant, human/LLM-readable events are written to the stream.
/// High-frequency telemetry data is intentionally excluded.
/// </remarks>
public record CommsEvent
{
    /// <summary>
    /// Identifies the subsystem that produced the event (e.g. <c>"SensorUpdate"</c>,
    /// <c>"DeviceAlert"</c>, <c>"ScheduledTask"</c>).
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Human/LLM-readable description of the event, suitable for direct inclusion
    /// in an agent prompt.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// UTC timestamp when the event was produced.
    /// </summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Optional structured JSON payload providing additional context for the agent.
    /// </summary>
    public string? JsonPayload { get; init; }
}
#endif
