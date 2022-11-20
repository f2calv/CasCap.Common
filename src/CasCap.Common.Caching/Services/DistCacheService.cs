using AsyncKeyedLock;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
namespace CasCap.Services;

public interface IDistCacheService
{
    event EventHandler<PostEvictionEventArgs> PostEvictionEvent;

    //todo:create 2x overload options to accept ttl(expiry) of a utc datetime
    Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class;
    Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class;

    Task Set<T>(string key, T cacheEntry, int ttl = -1) where T : class;
    Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class;

    Task Delete(string key);
    void DeleteLocal(string key, bool viaPubSub = false);
}

/// <summary>
/// Distributed cache service uses both an in-memory cache (local) AND a remote Redis cache (shared).
/// </summary>
public class DistCacheService : IDistCacheService
{
    readonly ILogger _logger;
    readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    readonly CachingOptions _cachingOptions;
    readonly IRedisCacheService _redis;
    readonly IMemoryCache _local;

    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    public DistCacheService(ILogger<DistCacheService> logger,
        IOptions<CachingOptions> cachingOptions,
        AsyncKeyedLocker<string> asyncKeyedLocker,
        IRedisCacheService redis//,
                                //IMemoryCache local
        )
    {
        _logger = logger;
        _asyncKeyedLocker = asyncKeyedLocker;
        _cachingOptions = cachingOptions.Value;
        _redis = redis;
        //_local = local;
        //todo:consider a Flags to disable use of local/shared memory in (console) applications that don't need it?
        _local = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _cachingOptions.MemoryCacheSizeLimit,
        });
    }

    //todo:store a summary of all cached items in a local lookup dictionary?
    //public ConcurrentDictionary<string, object> dItems { get; set; } = new();

    public Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
        => Get(key.CacheKey, createItem, ttl);

    public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
    {
        if (!_local.TryGetValue(key, out T? cacheEntry))
        {
            var tpl = await _redis.GetCacheEntryWithTTL<T>(key);
            if (tpl != default)
            {
                _logger.LogTrace("retrieved {key} object type {type} from shared cache", key, typeof(T));
                cacheEntry = tpl.cacheEntry;
                SetLocal(key, cacheEntry, tpl.expiry);
            }
            else if (createItem is not null)
            {
                //if we use Func and go create the cacheEntry, then we lock here to prevent multiple creations occurring at the same time
                using (await _asyncKeyedLocker.LockAsync(key).ConfigureAwait(false))
                {
                    // Key not in cache, so get data.
                    cacheEntry = await createItem();
                    _logger.LogTrace("setting {key} object type {type} in local cache", key, typeof(T));
                    if (cacheEntry is not null)
                    {
                        await Set(key, cacheEntry, ttl);
                    }
                }
            }
        }
        else
            _logger.LogTrace("retrieved {key} object type {type} from local cache", key, typeof(T));
        return cacheEntry;
    }

    public Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class
        => Set(key.CacheKey, cacheEntry, ttl);

    public async Task Set<T>(string key, T cacheEntry, int ttl = -1) where T : class
    {
        var expiry = ttl.GetExpiry();

        var bytes = cacheEntry.ToMessagePack();
        _ = await _redis.SetAsync(key, bytes, expiry);

        SetLocal(key, cacheEntry, expiry);
    }

    void SetLocal<T>(string key, T cacheEntry, TimeSpan? expiry) where T : class
    {
        var options = new MemoryCacheEntryOptions()
            // Pin to cache.
            .SetPriority(CacheItemPriority.Normal)
            // Set cache entry size by extension method.
            .SetSize(1)
            // Add eviction callback
            .RegisterPostEvictionCallback(EvictionCallback/*, this or cacheEntry.GetType()*/)
            ;
        if (expiry.HasValue)
            options.SetAbsoluteExpiration(expiry.Value);
        _ = _local.Set(key, cacheEntry, options);
        _logger.LogTrace("set {key} in local cache", key);
    }

    public async Task Delete(string key)
    {
        DeleteLocal(key, false);
        _ = await _redis.DeleteAsync(key, CommandFlags.FireAndForget);
        _redis.subscriber.Publish(_cachingOptions.ChannelName, $"{_cachingOptions.pubSubPrefix}{key}", CommandFlags.FireAndForget);
        _logger.LogDebug("removed {key} from local+shared cache, expiration message sent via pub/sub", key);
    }

    public void DeleteLocal(string key, bool viaPubSub)
    {
        _local.Remove(key);
        if (viaPubSub)
            _logger.LogDebug("removed {key} from local cache via pub/sub", key);
    }

    void EvictionCallback(object key, object value, EvictionReason reason, object state)
    {
        var args = new PostEvictionEventArgs(key, value, reason, state);
        OnRaisePostEvictionEvent(args);
        _logger.LogTrace("evicted {key} from local cache, reason {reason}", args.key, args.reason);
    }
}