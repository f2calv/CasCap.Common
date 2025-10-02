namespace CasCap.Models;

public enum CacheType
{
    None = 0,
    Memory = 1,
    Disk = 2,
    Redis = 4,
    //ValKey
    //Postgres
}

public enum SerializationType
{
    None = 0,
    Json = 1,
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
