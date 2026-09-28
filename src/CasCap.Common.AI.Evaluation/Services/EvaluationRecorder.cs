using System.Collections.Concurrent;

namespace CasCap.Common.Services;

/// <summary>Collects the tool calls and model round trips of a single evaluation run across every agent.</summary>
public sealed class EvaluationRecorder
{
    private readonly ConcurrentQueue<RecordedToolCall> _toolCalls = new();
    private readonly ConcurrentQueue<ModelRoundTrip> _roundTrips = new();

    /// <summary>Tool calls in the order they were made.</summary>
    public IReadOnlyList<RecordedToolCall> ToolCalls => [.. _toolCalls];

    /// <summary>Model round trips in the order they completed.</summary>
    public IReadOnlyList<ModelRoundTrip> RoundTrips => [.. _roundTrips];

    /// <summary>Records a tool call.</summary>
    /// <param name="toolCall">The call to record.</param>
    public void Add(RecordedToolCall toolCall) => _toolCalls.Enqueue(toolCall);

    /// <summary>Records a model round trip.</summary>
    /// <param name="roundTrip">The round trip to record.</param>
    public void Add(ModelRoundTrip roundTrip) => _roundTrips.Enqueue(roundTrip);
}
