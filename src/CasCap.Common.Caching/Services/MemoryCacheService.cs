namespace CasCap.Services;

public class MemoryCacheService : ILocalCacheService
{
    readonly ILogger _logger;
    readonly CachingOptions _cachingOptions;
    readonly MemoryCache _localCache;

    readonly HashSet<string> _cacheKeys = [];

    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    public MemoryCacheService(ILogger<MemoryCacheService> logger, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
        _localCache = new MemoryCache(new MemoryCacheOptions
        {
            //Clock,
            //CompactionPercentage
            //ExpirationScanFrequency
            SizeLimit = _cachingOptions.MemoryCacheSizeLimit,
            //TrackLinkedCacheEntries
            //TrackStatistics
        });
        if (_cachingOptions.MemoryCache.ClearOnStartup) DeleteAll();
    }

    public T? Get<T>(string key)
    {
        _localCache.TryGetValue(key, out T? cacheEntry);
        return cacheEntry;
    }

    public void Set<T>(string key, T cacheEntry, TimeSpan? expiry = null)
    {
        var options = new MemoryCacheEntryOptions()
            // Pin to cache.
            .SetPriority(_cachingOptions.MemoryCacheItemPriority)
            // Set cache entry size by extension method.
            .SetSize(1)
            // Add eviction callback
            .RegisterPostEvictionCallback(EvictionCallback!/*, this or cacheEntry.GetType()*/)
            ;
        if (expiry.HasValue)
            options.SetAbsoluteExpiration(expiry.Value);
        _ = _localCache.Set(key, cacheEntry, options);
        _cacheKeys.Add(key);
        _logger.LogTrace("{serviceName} set {key} in local cache", nameof(MemoryCacheService), key);
    }

    void EvictionCallback(object key, object value, EvictionReason reason, object state)
    {
        _cacheKeys.Remove((string)key);
        var args = new PostEvictionEventArgs(key, value, reason, state);
        OnRaisePostEvictionEvent(args);
        _logger.LogTrace("{serviceName} evicted {key} from local cache, reason {reason}",
            nameof(MemoryCacheService), args.key, args.reason);
    }

    public bool Delete(string key)
    {
        _localCache.TryGetValue(key, out object? cacheEntry);
        if (cacheEntry is not null)
        {
            _localCache.Remove(key);
            _cacheKeys.Remove(key);
            return true;
        }
        return false;
    }

    public long DeleteAll()
    {
        var i = 0L;
        foreach (var cacheKey in _cacheKeys)
        {
            if (Delete(cacheKey)) i++;
        }
        return i;
    }
}
