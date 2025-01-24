namespace CasCap.Services;

/// <summary>
/// The <see cref="MemoryCacheService"/> is an implementation of the <see cref="ILocalCache"/> which
/// acts as a wrapper around key functionality of the <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/> API.
/// </summary>
public class MemoryCacheService : ILocalCache
{
    private readonly ILogger _logger;
    private readonly CachingOptions _cachingOptions;
    private readonly MemoryCache _localCache;

    private readonly ConcurrentDictionary<string, int> _cacheKeys = [];

    /// <inheritdoc/>
    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public T? Get<T>(string key)
    {
        if (_localCache.TryGetValue(key, out T? cacheEntry))
            _logger.LogTrace("{className} retrieved object with {key} from local cache", nameof(MemoryCacheService), key);
        else
            _logger.LogTrace("{className} could not retrieve object with {key} from local cache", nameof(MemoryCacheService), key);
        return cacheEntry;
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T cacheEntry, TimeSpan? relativeExpiration = null, TimeSpan? absoluteExpiration = null)
    {
        var options = new MemoryCacheEntryOptions()
            // Pin to cache.
            .SetPriority(_cachingOptions.MemoryCacheItemPriority)
            // Set cache entry size by extension method.
            .SetSize(1)
            // Add eviction callback
            .RegisterPostEvictionCallback(EvictionCallback!/*, this or cacheEntry.GetType()*/)
            ;
        if (relativeExpiration.HasValue)
            options.SetSlidingExpiration(relativeExpiration.Value);
        if (absoluteExpiration.HasValue)
            options.SetAbsoluteExpiration(absoluteExpiration.Value);
        _ = _localCache.Set(key, cacheEntry, options);
        _cacheKeys.TryAdd(key, 0);
        _logger.LogTrace("{className} stored object with {key} in local cache (expiry {expiry})", nameof(MemoryCacheService), key, absoluteExpiration);
    }

    void EvictionCallback(object key, object value, EvictionReason reason, object state)
    {
        if (_cacheKeys.TryGetValue((string)key, out var ignore))
        {
            var args = new PostEvictionEventArgs(key, value, reason, state);
            OnRaisePostEvictionEvent(args);
            _logger.LogTrace("{className} evicted object with {key} from local cache (reason {reason})",
                nameof(MemoryCacheService), args.key, args.reason);
        }
    }

    /// <inheritdoc/>
    public bool Delete(string key)
    {
        _localCache.TryGetValue(key, out object? cacheEntry);
        if (cacheEntry is not null)
        {
            _localCache.Remove(key);
            _cacheKeys.TryRemove(key, out var val);
            _logger.LogTrace("{className} deleted object with {key} from local cache", nameof(MemoryCacheService), key);
            return true;
        }
        else
            _logger.LogTrace("{className} could not delete object with {key} from local cache (not present)", nameof(MemoryCacheService), key);
        return false;
    }

    /// <inheritdoc/>
    public long DeleteAll()
    {
        var i = 0L;
        foreach (var cacheKey in _cacheKeys.Keys)
        {
            if (Delete(cacheKey)) i++;
        }
        return i;
    }
}
