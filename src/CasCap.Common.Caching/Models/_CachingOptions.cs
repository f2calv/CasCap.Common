namespace CasCap.Models;

/// <summary>
/// Configuration options for the CasCap distributed caching system.
/// </summary>
public record CachingOptions
{
    /// <summary>
    /// Configuration sub-section locator key.
    /// </summary>
    public const string ConfigurationSectionName = $"{nameof(CasCap)}:{nameof(CachingOptions)}";

    /// <summary>
    /// <see cref="LocalCacheExpiryService"/> requires a unique prefix for all messages sent via the pub/sub
    /// channel so that the current instance doesn't take any action on self-generated messages.
    /// </summary>
    /// <remarks>
    /// This prefix can be customized.
    /// </remarks>
    public string PubSubPrefix { get; init; } = $"{Environment.MachineName}-{AppDomain.CurrentDomain.FriendlyName}";

    /// <summary>
    /// Gets or sets the maximum size of the cache, default is no limit.
    /// </summary>
    public int? MemoryCacheSizeLimit { get; set; } = null;

    /// <summary>
    /// Specifies how items are prioritized for preservation during a local memory pressure triggered cleanup.
    /// </summary>
    public CacheItemPriority MemoryCacheItemPriority { get; set; } = CacheItemPriority.Normal;

    /// <summary>
    /// Enables loading of built-in Lua scripts into Redis on startup.
    /// </summary>
    public bool UseBuiltInLuaScripts { get; set; } = false;

    /// <summary>
    /// Configuration options for the in-process <see cref="ILocalCache"/> memory cache.
    /// </summary>
    public CacheOptions MemoryCache { get; set; } = new CacheOptions { SerializationType = SerializationType.None };

    /// <summary>
    /// Configuration options for the disk-based <see cref="ILocalCache"/> cache.
    /// </summary>
    public CacheOptions DiskCache { get; set; } = new CacheOptions { SerializationType = SerializationType.Json };

    /// <summary>
    /// Configuration options for the <see cref="IRemoteCache"/> (Redis).
    /// </summary>
    public CacheOptions RemoteCache { get; set; } = new CacheOptions { SerializationType = SerializationType.MessagePack };

    /// <summary>
    /// Specifies the root folder where the local disk cache will store serialized files.
    /// </summary>
    public string DiskCacheFolder { get; init; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

    /// <summary>
    /// Enables pub/sub-based invalidation of local cache entries across distributed clients.
    /// </summary>
    public bool LocalCacheInvalidationEnabled { get; set; } = true;

    /// <summary>
    /// Controls how local sliding expirations are synchronized with the remote cache.
    /// </summary>
    public ExpirationSyncType ExpirationSyncMode { get; set; } = ExpirationSyncType.None;
}
