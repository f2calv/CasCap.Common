namespace CasCap.Models;

/// <summary>
/// Event arguments raised after a cache entry is evicted from the local <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/>.
/// </summary>
public class PostEvictionEventArgs(object key, object value, EvictionReason reason, object state) : EventArgs
{
    /// <summary>The cache key of the evicted entry.</summary>
    public object Key { get; } = key;

    /// <summary>The value of the evicted entry.</summary>
    public object Value { get; } = value;

    /// <summary>The reason the entry was evicted.</summary>
    public EvictionReason Reason { get; } = reason;

    /// <summary>The state associated with the eviction callback.</summary>
    public object State { get; } = state;
}
