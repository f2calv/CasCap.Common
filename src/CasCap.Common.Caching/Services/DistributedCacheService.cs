namespace CasCap.Services;

/// <summary>
/// Distributed cache service uses both a local cache (dotnet in-memory or disk) and a remote (Redis) cache.
/// </summary>
public class DistributedCacheService(ILogger<DistributedCacheService> logger,
    IOptions<CachingOptions> cachingOptions,
    IRemoteCache remoteCache,
    ILocalCache localCache) : IDistributedCache
{
    readonly CachingOptions _cachingOptions = cachingOptions.Value;

    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    //todo: store a summary of all cached items in a local lookup dictionary?
    //public ConcurrentDictionary<string, object> dItems { get; set; } = new();

    //public Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
    //    => Get(key.CacheKey, createItem, ttl);

    public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1, CommandFlags flags = CommandFlags.None) where T : class
    {
        T? cacheEntry = localCache.Get<T>(key);
        if (cacheEntry is null)
        {
            logger.LogTrace("{className} unable to retrieve {key} object type {type} from local cache",
                nameof(DistributedCacheService), key, typeof(T));
            if (_cachingOptions.RemoteCache.IsEnabled)
            {
                var tpl = await remoteCache.GetCacheEntryWithTTL<T>(key, flags);
                if (tpl != default && tpl.cacheEntry is not null)
                {
                    logger.LogTrace("{className} retrieved {key} object type {type} from remote cache",
                        nameof(DistributedCacheService), key, typeof(T));
                    cacheEntry = tpl.cacheEntry;
                    localCache.Set(key, cacheEntry, tpl.expiry);
                }
                else
                    logger.LogTrace("{className} unable to retrieve {key} object type {type} from remote cache",
                        nameof(DistributedCacheService), key, typeof(T));
            }
            //if cacheEntry is still null so now create it
            if (cacheEntry is null && createItem is not null)
            {
                //we lock here to prevent multiple creations occurring at the same time in the current application
                //TODO: integrate Redlock here?
                using (await AsyncDuplicateLock.LockAsync(key).ConfigureAwait(false))
                {
                    // Key not in cache, so get data.
                    cacheEntry = await createItem();
                    if (cacheEntry is not null)
                        await Set(key, cacheEntry, ttl);
                }
            }
        }
        else if (cacheEntry is not null)
            logger.LogTrace("{className} retrieved {key} object type {type} from local cache",
                nameof(DistributedCacheService), key, typeof(T));
        return cacheEntry;
    }

    //public Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class
    //    => Set(key.CacheKey, cacheEntry, ttl);

    public async Task Set<T>(string key, T cacheEntry, int ttl = -1, CommandFlags flags = CommandFlags.None) where T : class
    {
        var expiry = ttl.GetExpiry();

        if (_cachingOptions.RemoteCache.IsEnabled)
        {
            logger.LogTrace("{className} storing {key} object type {type} in remote cache",
                nameof(DistributedCacheService), key, typeof(T));
            if (_cachingOptions.RemoteCache.SerializationType == SerializationType.Json)
            {
                var json = cacheEntry.ToJson();
                _ = await remoteCache.SetAsync(key, json, expiry, flags);
                await Invalidate(key);
            }
            else if (_cachingOptions.RemoteCache.SerializationType == SerializationType.MessagePack)
            {
                var bytes = cacheEntry.ToMessagePack();
                _ = await remoteCache.SetAsync(key, bytes, expiry, flags);
                await Invalidate(key);
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.RemoteCache.SerializationType)} {_cachingOptions.RemoteCache.SerializationType} is not supported!");
        }

        localCache.Set(key, cacheEntry, expiry);
    }

    public async Task<bool> Delete(string key, CommandFlags flags = CommandFlags.FireAndForget)
    {
        var result1 = localCache.Delete(key);
        var result2 = false;
        if (_cachingOptions.RemoteCache.IsEnabled)
            result2 = await remoteCache.DeleteAsync(key, flags);
        await Invalidate(key);
        return result1 || result2;
    }

    private async Task Invalidate(string key, CommandFlags flags = CommandFlags.FireAndForget)
    {
        if (_cachingOptions.RemoteCache.IsEnabled && _cachingOptions.LocalCacheInvalidationEnabled)
        {
            _ = await remoteCache.Subscriber.PublishAsync(RedisChannel.Literal(_cachingOptions.ChannelName),
                $"{_cachingOptions.PubSubPrefix}:{key}", flags);
            logger.LogTrace("{className} sent expiration message for {key} via pub/sub",
                nameof(DistributedCacheService), key);
        }
    }

    public async Task<long> DeleteAll(CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        throw new NotImplementedException("TODO!");
    }
}
