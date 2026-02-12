namespace CasCap.Models;

/// <summary>
/// Identifies the type of cache provider.
/// </summary>
public enum CacheType
{
    /// <summary>No cache.</summary>
    None = 0,
    /// <summary>In-process memory cache.</summary>
    Memory = 1,
    /// <summary>File-system disk cache.</summary>
    Disk = 2,
    /// <summary>Redis remote cache.</summary>
    Redis = 4,
    //ValKey
    //Postgres
}

/// <summary>
/// Identifies the serialization format used for cache entries.
/// </summary>
public enum SerializationType
{
    /// <summary>No serialization (in-memory objects only).</summary>
    None = 0,
    /// <summary>JSON serialization via System.Text.Json.</summary>
    Json = 1,
    /// <summary>Binary serialization via MessagePack.</summary>
    MessagePack = 2
}

/// <summary>
/// When retrieving an item from <see cref="ILocalCache"/> it's sliding expiration will be updated
/// automatically however the <see cref="IRemoteCache"/> will not know about this expiration extension
/// and will expire the local item sooner, we can handle that in a number of ways.
/// </summary>
public enum ExpirationSyncType
{
    /// <summary>
    /// No action is taken and there could be a disparity between the sliding expirations local vs. remote.
    /// </summary>
    None = 0,
    /// <summary>
    /// Let the <see cref="LocalCacheExpiryService"/> handle the expiration event on other connected clients.
    /// </summary>
    ExpireViaPubSub = 1,
    /// <summary>
    /// Extend remote expiry by setting an updated expiry time on the cached item.
    /// </summary>
    ExtendRemoteExpiry = 2
}
