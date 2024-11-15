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
    public string PubSubPrefix { get; } = $"{Environment.MachineName}-{AppDomain.CurrentDomain.FriendlyName}";

    /// <summary>
    /// All SET and DEL events are pushed to this channel prefixed with the PubSubPrefix of the local application.
    /// All other applications subscribe to this channel and expire any cache items which don't match their own PubSubPrefix.
    /// </summary>
    public string ChannelName { get; set; } = "expiration";
    
    ///// <summary>
    ///// Subscribe and process all keyspace events.
    ///// </summary>
    //public string ChannelName { get; set; } = "__keyspace@0__:*";

    /// <summary>
    /// Gets or sets the maximum size of the cache, default is no limit.
    /// </summary>
    public int? MemoryCacheSizeLimit { get; set; } = null;

    /// <summary>
    /// Specifies how items are prioritized for preservation during a local memory pressure triggered cleanup.
    /// </summary>
    public CacheItemPriority MemoryCacheItemPriority { get; set; } = CacheItemPriority.Normal;

    public bool LoadBuiltInLuaScripts { get; set; } = false;

    public CacheOptions MemoryCache { get; set; } = new CacheOptions { SerializationType = SerializationType.None };

    public CacheOptions DiskCache { get; set; } = new CacheOptions { SerializationType = SerializationType.Json };

    public CacheOptions RemoteCache { get; set; } = new CacheOptions { SerializationType = SerializationType.MessagePack };

    /// <summary>
    /// Specifies the root folder where the local disk cache will store serialized files.
    /// </summary>
    public string DiskCacheFolder { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

    public bool LocalCacheInvalidationEnabled { get; set; } = true;
}

public class CacheOptions
{
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
