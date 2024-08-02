namespace CasCap.Services;

public class MemoryCacheService : ILocalCacheService
{
    readonly ILogger _logger;
    readonly CachingOptions _cachingOptions;
    readonly IMemoryCache _localCache;

    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    public MemoryCacheService(ILogger<MemoryCacheService> logger, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
        //todo:consider a Flags to disable use of local and/or remote caches in (console?) applications that don't need either
        _localCache = new MemoryCache(new MemoryCacheOptions
        {
            //Clock,
            //CompactionPercentage
            //ExpirationScanFrequency
            SizeLimit = _cachingOptions.MemoryCacheSizeLimit,
            //TrackLinkedCacheEntries
            //TrackStatistics
        });
    }

    public T? Get<T>(string key)
    {
        _localCache.TryGetValue(key, out T? cacheEntry);
        return cacheEntry;
    }

    public void SetLocal<T>(string key, T cacheEntry, TimeSpan? expiry)
    {
        var options = new MemoryCacheEntryOptions()
            // Pin to cache.
            .SetPriority(_cachingOptions.MemoryCacheItemPriority)
            // Set cache entry size by extension method.
            .SetSize(1)
            // Add eviction callback
            .RegisterPostEvictionCallback(EvictionCallback/*, this or cacheEntry.GetType()*/)
            ;
        if (expiry.HasValue)
            options.SetAbsoluteExpiration(expiry.Value);
        _ = _localCache.Set(key, cacheEntry, options);
        _logger.LogTrace("{serviceName} set {key} in local cache", nameof(MemoryCacheService), key);
    }

    void EvictionCallback(object key, object value, EvictionReason reason, object state)
    {
        var args = new PostEvictionEventArgs(key, value, reason, state);
        OnRaisePostEvictionEvent(args);
        _logger.LogTrace("{serviceName} evicted {key} from local cache, reason {reason}",
            nameof(MemoryCacheService), args.key, args.reason);
    }

    public void DeleteLocal(string key, bool viaPubSub)
    {
        _localCache.Remove(key);
        if (viaPubSub)
            _logger.LogTrace("{serviceName} removed {key} from local cache via pub/sub", nameof(MemoryCacheService), key);
    }
}