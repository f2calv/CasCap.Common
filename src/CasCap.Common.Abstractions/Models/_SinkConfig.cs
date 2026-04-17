#if NET8_0_OR_GREATER
using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Abstractions;

/// <summary>
/// Configuration options for event sink registration.
/// Each entry key is matched against <see cref="SinkTypeAttribute.SinkType"/>
/// on <see cref="IEventSink{T}"/> implementations.
/// </summary>
public record SinkConfig
{
    /// <summary>
    /// Dictionary of available sink types and their configuration.
    /// Keys correspond to <see cref="SinkTypeAttribute.SinkType"/> values
    /// (e.g. "Console", "Redis", "AzureTables", "Metrics").
    /// </summary>
    [Required, MinLength(1)]
    public required Dictionary<string, SinkConfigParams> AvailableSinks { get; init; } = new()
    {
        ["Console"] = new() { Enabled = true },
    };

    /// <summary>
    /// Returns a copy of this configuration with the specified sink type disabled.
    /// Used in lite registration scenarios to exclude sinks that require heavy
    /// infrastructure dependencies.
    /// </summary>
    /// <param name="sinkType">The sink type key to disable (e.g. "Redis").</param>
    public SinkConfig WithoutSinkType(string sinkType) =>
        AvailableSinks.TryGetValue(sinkType, out var existing) && existing.Enabled
            ? this with { AvailableSinks = new(AvailableSinks) { [sinkType] = existing with { Enabled = false } } }
            : this;
}
#endif
