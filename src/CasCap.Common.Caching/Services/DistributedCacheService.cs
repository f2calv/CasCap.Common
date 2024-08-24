namespace CasCap.Services;

/// <summary>
/// Distributed cache service uses both a local cache (dotnet in-memory or disk) and a remote (Redis) cache.
/// </summary>
public class DistributedCacheService(ILogger<DistributedCacheService> logger,
    IOptions<CachingOptions> cachingOptions,
    IRemoteCacheService remoteCacheSvc,
    ILocalCacheService localCacheSvc) : IDistributedCacheService
{
    readonly CachingOptions _cachingOptions = cachingOptions.Value;

    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    //todo: store a summary of all cached items in a local lookup dictionary?
    //public ConcurrentDictionary<string, object> dItems { get; set; } = new();

    //public Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
    //    => Get(key.CacheKey, createItem, ttl);

    public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
    {
        T? cacheEntry = localCacheSvc.Get<T>(key);
        if (cacheEntry is null)
        {
            var tpl = await remoteCacheSvc.GetCacheEntryWithTTL<T>(key);
            if (tpl != default)
            {
                logger.LogTrace("{serviceName} retrieved {key} object type {type} from remote cache",
                    nameof(DistributedCacheService), key, typeof(T));
                cacheEntry = tpl.cacheEntry;
                localCacheSvc.Set(key, cacheEntry, tpl.expiry);
            }
            else if (createItem is not null)
            {
                //we lock here to prevent multiple creations occurring at the same time
                //TODO: integrate Redlock here
                using (await AsyncDuplicateLock.LockAsync(key).ConfigureAwait(false))
                {
                    // Key not in cache, so get data.
                    cacheEntry = await createItem();
                    logger.LogTrace("{serviceName} setting {key} object type {type} in local cache",
                        nameof(DistributedCacheService), key, typeof(T));
                    if (cacheEntry is not null)
                    {
                        await Set(key, cacheEntry, ttl);
                    }
                }
            }
        }
        else if (cacheEntry is not null)
            logger.LogTrace("{serviceName} retrieved {key} object type {type} from local cache",
                nameof(DistributedCacheService), key, typeof(T));
        return cacheEntry;
    }

    //public Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class
    //    => Set(key.CacheKey, cacheEntry, ttl);

    public async Task Set<T>(string key, T cacheEntry, int ttl = -1) where T : class
    {
        var expiry = ttl.GetExpiry();

        if (_cachingOptions.RemoteCache.SerializationType == SerializationType.Json)
        {
            var json = cacheEntry.ToJSON();
            _ = await remoteCacheSvc.SetAsync(key, json, expiry);
        }
        else if (_cachingOptions.RemoteCache.SerializationType == SerializationType.MessagePack)
        {
            var bytes = cacheEntry.ToMessagePack();
            _ = await remoteCacheSvc.SetAsync(key, bytes, expiry);
        }
        else
            throw new NotSupportedException($"{nameof(_cachingOptions.RemoteCache.SerializationType)} {_cachingOptions.RemoteCache.SerializationType} is not supported!");

        localCacheSvc.Set(key, cacheEntry, expiry);
    }

    public async Task<bool> Delete(string key)
    {
        var result1 = localCacheSvc.Delete(key);
        var result2 = await remoteCacheSvc.DeleteAsync(key, CommandFlags.FireAndForget);

        if (_cachingOptions.LocalCacheInvalidationEnabled)
        {
            _ = await remoteCacheSvc.Subscriber.PublishAsync(RedisChannel.Literal(_cachingOptions.ChannelName), $"{_cachingOptions.pubSubPrefix}:{key}", CommandFlags.FireAndForget);
            logger.LogTrace("{serviceName} removed {key} from local+remote cache, expiration message sent via pub/sub",
                nameof(DistributedCacheService), key);
        }
        return result1 || result2;
    }

    public async Task<long> DeleteAll(CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        throw new NotImplementedException("TODO!");
    }
}
