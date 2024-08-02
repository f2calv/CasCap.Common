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
}

/// <summary>
/// Distributed cache service uses both local cache (dotnet in-memory) and remote (Redis) caches.
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    readonly ILogger _logger;
    readonly CachingOptions _cachingOptions;
    readonly IRemoteCacheService _remoteCacheSvc;
    readonly ILocalCacheService _localCacheSvc;

    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    public DistributedCacheService(ILogger<DistributedCacheService> logger,
        IOptions<CachingOptions> cachingOptions,
        IRemoteCacheService remoteCacheSvc,
        IEnumerable<ILocalCacheService> localCacheSvcs)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
        //todo:consider a Flags to disable use of local and/or remote caches in (console?) applications that don't need either
        _remoteCacheSvc = remoteCacheSvc;
        foreach (var localCache in localCacheSvcs)
        {
            if (_cachingOptions.LocalCacheType == LocalCacheType.Disk && localCache.GetType() == typeof(DiskCacheService))
                _localCacheSvc = localCache;
            else if (_cachingOptions.LocalCacheType == LocalCacheType.Memory && localCache.GetType() == typeof(MemoryCacheService))
                _localCacheSvc = localCache;
            if (_localCacheSvc is not null) break;
        }
        if (_localCacheSvc is null) throw new NotSupportedException();
    }

    //todo:store a summary of all cached items in a local lookup dictionary?
    //public ConcurrentDictionary<string, object> dItems { get; set; } = new();

    public Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
        => Get(key.CacheKey, createItem, ttl);

    public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
    {
        var cacheEntry = _localCacheSvc.Get<T>(key);
        if (cacheEntry is null)
        {
            var tpl = await _remoteCacheSvc.GetCacheEntryWithTTL<T>(key);
            if (tpl != default)
            {
                _logger.LogTrace("{serviceName} retrieved {key} object type {type} from remote cache",
                    nameof(DistributedCacheService), key, typeof(T));
                cacheEntry = tpl.cacheEntry;
                _localCacheSvc.SetLocal(key, cacheEntry, tpl.expiry);
            }
            else if (createItem is not null)
            {
                //if we use Func and go create the cacheEntry, then we lock here to prevent multiple creations occurring at the same time
                using (await AsyncDuplicateLock.LockAsync(key).ConfigureAwait(false))
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

        _localCacheSvc.SetLocal(key, cacheEntry, expiry);
    }

    public async Task Delete(string key)
    {
        _localCacheSvc.DeleteLocal(key, false);
        _ = await _remoteCacheSvc.DeleteAsync(key, CommandFlags.FireAndForget);
        _ = await _remoteCacheSvc.subscriber.PublishAsync(RedisChannel.Literal(_cachingOptions.ChannelName), $"{_cachingOptions.pubSubPrefix}{key}", CommandFlags.FireAndForget);
        _logger.LogDebug("{serviceName} removed {key} from local+remote cache, expiration message sent via pub/sub",
            nameof(DistributedCacheService), key);
    }
}
