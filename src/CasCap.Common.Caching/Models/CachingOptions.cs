namespace CasCap.Models;

public class CachingOptions
{
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

    public LocalCacheType LocalCacheType { get; set; } = LocalCacheType.Memory;

    public string CacheRoot { get; set; } = null!;
}

public enum LocalCacheType
{
    Memory,
    Disk
}