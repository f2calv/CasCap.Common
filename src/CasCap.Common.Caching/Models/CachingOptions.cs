namespace CasCap.Models;

public class CachingOptions
{
    public const string SectionKey = $"{nameof(CasCap)}:{nameof(CachingOptions)}";

    /// <summary>
    /// prefix all keys sent via pub/sub with a unique identifier so that when an app is connected
    /// as both pub+sub it doesn't duplicate handling of it's own expiration messages
    /// </summary>
    public string pubSubPrefix { get; } = $"{Environment.MachineName}_{AppDomain.CurrentDomain.FriendlyName}_";

    public string ChannelName { get; set; } = "expiration";

    public int MemoryCacheSizeLimit { get; set; }
}
