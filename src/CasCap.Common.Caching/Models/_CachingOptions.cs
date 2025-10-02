namespace CasCap.Models;

public record CachingOptions
{
    /// <summary>
    /// Configuration sub-section locator key.
    /// </summary>
    public const string SectionKey = $"{nameof(CasCap)}:{nameof(CachingOptions)}";

    /// <summary>
    /// <see cref="LocalCacheExpiryService"/> requires a unique prefix for all messages sent via the pub/sub
    /// channel so that the current instance doesn't take any action on self-generated messages.
    /// </summary>
    /// <remarks>
    /// This prefix can be customized.
    /// </remarks>
    public string PubSubPrefix { get; } = $"{Environment.MachineName}-{AppDomain.CurrentDomain.FriendlyName}";

    /// <summary>
    /// Gets or sets the maximum size of the cache, default is no limit.
    /// </summary>
    public int? MemoryCacheSizeLimit { get; set; } = null;

    /// <summary>
    /// Specifies how items are prioritized for preservation during a local memory pressure triggered cleanup.
    /// </summary>
    public CacheItemPriority MemoryCacheItemPriority { get; set; } = CacheItemPriority.Normal;

    public bool UseBuiltInLuaScripts { get; set; } = false;

    public CacheOptions MemoryCache { get; set; } = new CacheOptions { SerializationType = SerializationType.None };

    public CacheOptions DiskCache { get; set; } = new CacheOptions { SerializationType = SerializationType.Json };

    public CacheOptions RemoteCache { get; set; } = new CacheOptions { SerializationType = SerializationType.MessagePack };

    /// <summary>
    /// Specifies the root folder where the local disk cache will store serialized files.
    /// </summary>
    public string DiskCacheFolder { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

    public bool LocalCacheInvalidationEnabled { get; set; } = true;

    public ExpirationSyncType ExpirationSyncMode { get; set; } = ExpirationSyncType.None;
}

public class CacheOptions
{
    public bool IsEnabled { get; set; } = true;

    public int DatabaseId { get; set; } = 0;

    public bool ClearOnStartup { get; set; } = false;

    public SerializationType SerializationType { get; set; } = SerializationType.MessagePack;
}

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
