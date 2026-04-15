namespace CasCap.Common.Models;

/// <summary>Configuration options for an individual cache layer (memory, disk or remote).</summary>
public record CacheParameters
{
    /// <summary>Whether this cache layer is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>The Redis database index to use for the remote cache.</summary>
    [Range(0, 15)]
    public int DatabaseId { get; init; } = 0;

    /// <summary>Whether to clear all cached items when the service starts.</summary>
    public bool ClearOnStartup { get; init; } = false;

    /// <summary>The serialization format used for cache entries in this layer.</summary>
    public SerializationType SerializationType { get; init; } = SerializationType.MessagePack;
}
