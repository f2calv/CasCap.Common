namespace CasCap.Common.Models;

/// <summary>
/// Configuration options for the CasCap distributed caching system.
/// </summary>
public record CachingConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(CachingConfig)}";

    /// <summary>Redis connection string for the remote cache and distributed locking.</summary>
    /// <remarks>When set, this is used as the default connection string by <c>AddCasCapCaching</c>.</remarks>
    public string? RemoteCacheConnectionString { get; init; }

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
    public CacheParameters MemoryCache { get; set; } = new CacheParameters { SerializationType = SerializationType.None };

    /// <summary>
    /// Configuration options for the disk-based <see cref="ILocalCache"/> cache.
    /// </summary>
    public CacheParameters DiskCache { get; set; } = new CacheParameters { SerializationType = SerializationType.Json };

    /// <summary>
    /// Configuration options for the <see cref="IRemoteCache"/> (Redis).
    /// </summary>
    public CacheParameters RemoteCache { get; set; } = new CacheParameters { SerializationType = SerializationType.MessagePack };

    /// <summary>
    /// Specifies the root folder where the local disk cache will store serialized files.
    /// </summary>
    public string DiskCacheFolder { get; init; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

    /// <summary>
    /// Enables pub/sub-based invalidation of local cache entries across distributed clients.
    /// </summary>
    public bool LocalCacheInvalidationEnabled { get; set; } = true;

    /// <summary>Controls how local sliding expirations are synchronized with the remote cache.</summary>
    public ExpirationSyncType ExpirationSyncMode { get; set; } = ExpirationSyncType.None;

    /// <summary>Enables registration of <see cref="IDistributedLockFactory"/> for Redis-based distributed locking.</summary>
    public bool DistributedLockingEnabled { get; set; } = false;

    /// <summary>Format string for Redis distributed lock keys.</summary>
    /// <remarks>Defaults to <c>RedLock:{0}</c>. The <c>{0}</c> placeholder is replaced with the lock resource name.</remarks>
    public string RedisKeyFormat { get; init; } = "RedLock:{0}";

    /// <summary>Timing parameters for Redis distributed locks (Redlock algorithm).</summary>
    public RedlockConfig Redlock { get; set; } = new();
}
