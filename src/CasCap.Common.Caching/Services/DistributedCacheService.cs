﻿using AsyncKeyedLock;
using StackExchange.Redis;
namespace CasCap.Services;

public interface IDistributedCacheService
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
/// Distributed cache service uses both local cache (dotnet in-memory) and remote (Redis) caches.
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    readonly ILogger _logger;
    readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    readonly CachingOptions _cachingOptions;
    readonly IRemoteCacheService _remoteCacheSvc;
    readonly IMemoryCache _localCache;

    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    public DistributedCacheService(ILogger<DistributedCacheService> logger,
        AsyncKeyedLocker<string> asyncKeyedLocker,
        IOptions<CachingOptions> cachingOptions,
        IRemoteCacheService remoteCacheSvc)
    {
        _logger = logger;
        _asyncKeyedLocker = asyncKeyedLocker;
        _cachingOptions = cachingOptions.Value;
        _remoteCacheSvc = remoteCacheSvc;
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

    //todo:store a summary of all cached items in a local lookup dictionary?
    //public ConcurrentDictionary<string, object> dItems { get; set; } = new();

    public Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
        => Get(key.CacheKey, createItem, ttl);

    public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
    {
        if (!_localCache.TryGetValue(key, out T? cacheEntry))
        {
            var tpl = await _remoteCacheSvc.GetCacheEntryWithTTL<T>(key);
            if (tpl != default)
            {
                _logger.LogTrace("{serviceName} retrieved {key} object type {type} from remote cache",
                    nameof(DistributedCacheService), key, typeof(T));
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
                    _logger.LogTrace("{serviceName} setting {key} object type {type} in local cache",
                        nameof(DistributedCacheService), key, typeof(T));
                    if (cacheEntry is not null)
                    {
                        await Set(key, cacheEntry, ttl);
                    }
                }
            }
        }
        else
            _logger.LogTrace("{serviceName} retrieved {key} object type {type} from local cache",
                nameof(DistributedCacheService), key, typeof(T));
        return cacheEntry;
    }

    public Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class
        => Set(key.CacheKey, cacheEntry, ttl);

    public async Task Set<T>(string key, T cacheEntry, int ttl = -1) where T : class
    {
        var expiry = ttl.GetExpiry();

        var bytes = cacheEntry.ToMessagePack();
        _ = await _remoteCacheSvc.SetAsync(key, bytes, expiry);

        SetLocal(key, cacheEntry, expiry);
    }

    void SetLocal<T>(string key, T cacheEntry, TimeSpan? expiry) where T : class
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
        _logger.LogTrace("{serviceName} set {key} in local cache", nameof(DistributedCacheService), key);
    }

    public async Task Delete(string key)
    {
        DeleteLocal(key, false);
        _ = await _remoteCacheSvc.DeleteAsync(key, CommandFlags.FireAndForget);
        _ = await _remoteCacheSvc.subscriber.PublishAsync(RedisChannel.Literal(_cachingOptions.ChannelName), $"{_cachingOptions.pubSubPrefix}{key}", CommandFlags.FireAndForget);
        _logger.LogDebug("{serviceName} removed {key} from local+remote cache, expiration message sent via pub/sub",
            nameof(DistributedCacheService), key);
    }

    public void DeleteLocal(string key, bool viaPubSub)
    {
        _localCache.Remove(key);
        if (viaPubSub)
            _logger.LogDebug("{serviceName} removed {key} from local cache via pub/sub", nameof(DistributedCacheService), key);
    }

    void EvictionCallback(object key, object value, EvictionReason reason, object state)
    {
        var args = new PostEvictionEventArgs(key, value, reason, state);
        OnRaisePostEvictionEvent(args);
        _logger.LogTrace("{serviceName} evicted {key} from local cache, reason {reason}",
            nameof(DistributedCacheService), args.key, args.reason);
    }
}
