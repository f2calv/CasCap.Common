namespace CasCap.Services;

/// <summary>
/// The <see cref="DistributedCacheService"/> uses both <see cref="ILocalCache"/> and <see cref="IRemoteCache"/>
/// to implement <see cref="IDistributedCache"/>.
/// </summary>
public class DistributedCacheService(ILogger<DistributedCacheService> logger, IOptions<CachingOptions> cachingOptions,
    IRemoteCache remoteCache, ILocalCache localCache) : IDistributedCache
{
    private readonly CachingOptions _cachingOptions = cachingOptions.Value;

    /// <inheritdoc/>
    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
    /// <inheritdoc/>
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

    //todo: store a summary of all cached items in a local lookup dictionary?
    ///// <inheritdoc/>
    //public ConcurrentDictionary<string, object> dItems { get; set; } = new();

    /// <inheritdoc/>
    public Task<T?> Get<T>(string key) where T : class
        => Get<T>(key, createItem: null, slidingExpiration: null, absoluteExpiration: null, flags: CommandFlags.None);

    /// <inheritdoc/>
    public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None) where T : class
    {
        T? cacheEntry = localCache.Get<T>(key);
        if (cacheEntry is null)
        {
            logger.LogTrace("{className} unable to retrieve {key} object type {type} from {objectName}",
                nameof(DistributedCacheService), key, typeof(T), nameof(ILocalCache));
            if (_cachingOptions.RemoteCache.IsEnabled)
            {
                var tpl = await remoteCache.GetCacheEntryWithExpiryAsync<T>(key, flags);
                if (tpl != default && tpl.cacheEntry is not null)
                {
                    logger.LogTrace("{className} retrieved {key} object type {type} from {objectName}",
                        nameof(DistributedCacheService), key, typeof(T), nameof(IRemoteCache));
                    cacheEntry = tpl.cacheEntry;
                    localCache.Set(key, cacheEntry, tpl.expiry);
                }
                else
                    logger.LogTrace("{className} unable to retrieve {key} object type {type} from {objectName}",
                        nameof(DistributedCacheService), key, typeof(T), nameof(IRemoteCache));
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
                        await Set(key, cacheEntry, slidingExpiration, absoluteExpiration, flags);
                }
            }
        }
        else if (cacheEntry is not null)
        {
            logger.LogTrace("{className} retrieved {key} object type {type} from {objectName}",
                nameof(DistributedCacheService), key, typeof(T), nameof(ILocalCache));
            if (_cachingOptions.ExpirationSyncMode == ExpirationSyncType.ExtendRemoteExpiry)
                await remoteCache.ExtendSlidingExpirationAsync(key);
        }
        return cacheEntry;
    }

    /// <inheritdoc/>
    public Task Set<T>(string key, T cacheEntry) where T : class
        => Set(key, cacheEntry, slidingExpiration: null, absoluteExpiration: null, flags: CommandFlags.None);

    /// <inheritdoc/>
    public async Task Set<T>(string key, T cacheEntry, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None) where T : class
    {
        if (_cachingOptions.RemoteCache.IsEnabled)
        {
            logger.LogTrace("{className} storing {key} object type {type} in {objectName}",
                nameof(DistributedCacheService), key, typeof(T), nameof(IRemoteCache));
            if (_cachingOptions.RemoteCache.SerializationType == SerializationType.Json)
            {
                var json = cacheEntry.ToJson();
                _ = await remoteCache.SetAsync(key, json, slidingExpiration, absoluteExpiration, flags: flags);
                await InvalidateLocalCache(key);
            }
            else if (_cachingOptions.RemoteCache.SerializationType == SerializationType.MessagePack)
            {
                var bytes = cacheEntry.ToMessagePack();
                _ = await remoteCache.SetAsync(key, bytes, slidingExpiration, absoluteExpiration, flags: flags);
                await InvalidateLocalCache(key);
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.RemoteCache.SerializationType)} {_cachingOptions.RemoteCache.SerializationType} is not supported!");
        }

        localCache.Set(key, cacheEntry, slidingExpiration);
    }

    /// <inheritdoc/>
    public async Task<bool> Delete(string key, CommandFlags flags = CommandFlags.FireAndForget)
    {
        var result1 = localCache.Delete(key);
        var result2 = false;
        if (_cachingOptions.RemoteCache.IsEnabled)
            result2 = await remoteCache.DeleteAsync(key, flags);
        await InvalidateLocalCache(key);
        return result1 || result2;
    }

    /// <summary>
    /// When a change or update is made to a cached item in <see cref="DistributedCacheService"/> we must
    /// invalidate the locally cached item from all clients other than this one.
    /// All SET and DEL events are pushed to this channel prefixed with the local application+client id (PubSubPrefix).
    /// </summary>
    private async Task InvalidateLocalCache(string key, CommandFlags flags = CommandFlags.FireAndForget)
    {
        if (_cachingOptions.RemoteCache.IsEnabled && _cachingOptions.LocalCacheInvalidationEnabled)
        {
            _ = await remoteCache.Subscriber.PublishAsync(RedisChannel.Literal(nameof(LocalCacheExpiryService)),
                $"{_cachingOptions.PubSubPrefix}:{key}", flags);
            logger.LogTrace("{className} sent {abstractionName} expiration message for {key} via pub/sub",
                nameof(DistributedCacheService), nameof(ILocalCache), key);
        }
    }

    /// <inheritdoc/>
    public async Task<long> DeleteAll(CommandFlags flags = CommandFlags.None, CancellationToken cancellationToken = default)
    {
        await Task.Delay(0, cancellationToken);
        throw new NotImplementedException("TODO!");
    }
}
