namespace CasCap.Common.Abstractions;

/// <summary>
/// Marks an <see cref="IEventSink{T}"/> implementation with a sink type identifier used
/// to match against <see cref="SinkConfig.AvailableSinks"/> configuration entries.
/// </summary>
/// <param name="sinkType">The unique sink type identifier (e.g. "Console", "Redis", "AzureTables").</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SinkTypeAttribute(string sinkType) : Attribute
{
    /// <summary>The unique sink type identifier used to match against configuration.</summary>
    public string SinkType { get; } = sinkType;
}
