namespace CasCap.Models;

public class CachingOptions
{
    /// <summary>
    /// Configuration sub-section locator key.
    /// </summary>
    public const string SectionKey = $"{nameof(CasCap)}:{nameof(CachingOptions)}";

    /// <summary>
    /// Prefix all keys sent via pub/sub with a unique identifier so that when a single client is connected
    /// as both pub+sub it doesn't duplicate handling of it's own expiration messages.
    /// </summary>
    public string pubSubPrefix { get; } = $"{Environment.MachineName}_{AppDomain.CurrentDomain.FriendlyName}_";

    public string ChannelName { get; set; } = "expiration";

    /// <summary>
    /// Gets or sets the maximum size of the cache, default is no limit.
    /// </summary>
    public int? MemoryCacheSizeLimit { get; set; } = null;

    /// <summary>
    /// Specifies how items are prioritized for preservation during a local memory pressure triggered cleanup.
    /// </summary>
    public CacheItemPriority MemoryCacheItemPriority { get; set; } = CacheItemPriority.Normal;

    public bool LoadBuiltInLuaScripts { get; set; } = false;

    public CacheOptions DiskCache { get; set; } = new CacheOptions { SerialisationType = SerialisationType.Json };

    public CacheOptions RemoteCache { get; set; } = new CacheOptions { SerialisationType = SerialisationType.MessagePack };

    /// <summary>
    /// Specifies the root folder where the local disk cache will store serialised files.
    /// </summary>
    public string DiskCacheFolder { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

    public bool LocalCacheInvalidationEnabled { get; set; } = true;
}

public class CacheOptions
{
    public bool ClearOnStartup { get; set; } = false;
    public SerialisationType SerialisationType { get; set; } = SerialisationType.MessagePack;
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

public enum SerialisationType
{
    None = 0,
    Json = 1,
    MessagePack = 2
}
