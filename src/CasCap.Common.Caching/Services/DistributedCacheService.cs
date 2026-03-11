namespace CasCap.Common.Services;

/// <summary>
/// The <see cref="DistributedCacheService"/> uses both <see cref="ILocalCache"/> and <see cref="IRemoteCache"/>
/// to implement <see cref="IDistributedCache"/>.
/// </summary>
public class DistributedCacheService(ILogger<DistributedCacheService> logger, IOptions<CachingConfig> cachingConfig,
    IRemoteCache remoteCache, ILocalCache localCache) : IDistributedCache
{
    private readonly CachingConfig _cachingConfig = cachingConfig.Value;

    /// <inheritdoc/>
    public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;

    /// <summary>
    /// Raises the <see cref="PostEvictionEvent"/>.
    /// </summary>
    protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) => PostEvictionEvent?.Invoke(this, args);

    //TODO: store a summary of all cached items in a local lookup dictionary?
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
            logger.LogTrace("{ClassName} unable to retrieve {Key} object type {Type} from {ObjectName}",
                nameof(DistributedCacheService), key, typeof(T), nameof(ILocalCache));
            if (_cachingConfig.RemoteCache.IsEnabled)
            {
                var tpl = await remoteCache.GetCacheEntryWithExpiryAsync<T>(key, flags);
                if (tpl != default && tpl.cacheEntry is not null)
                {
                    logger.LogTrace("{ClassName} retrieved {Key} object type {Type} from {ObjectName}",
                        nameof(DistributedCacheService), key, typeof(T), nameof(IRemoteCache));
                    cacheEntry = tpl.cacheEntry;
                    localCache.Set(key, cacheEntry, tpl.expiry);
                }
                else
                    logger.LogTrace("{ClassName} unable to retrieve {Key} object type {Type} from {ObjectName}",
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
            logger.LogTrace("{ClassName} retrieved {Key} object type {Type} from {ObjectName}",
                nameof(DistributedCacheService), key, typeof(T), nameof(ILocalCache));
            if (_cachingConfig.ExpirationSyncMode == ExpirationSyncType.ExtendRemoteExpiry)
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
        if (_cachingConfig.RemoteCache.IsEnabled)
        {
            logger.LogTrace("{ClassName} storing {Key} object type {Type} in {ObjectName}",
                nameof(DistributedCacheService), key, typeof(T), nameof(IRemoteCache));
            if (_cachingConfig.RemoteCache.SerializationType == SerializationType.Json)
            {
                var json = cacheEntry.ToJson();
                _ = await remoteCache.SetAsync(key, json, slidingExpiration, absoluteExpiration, flags: flags);
                await InvalidateLocalCache(key);
            }
            else if (_cachingConfig.RemoteCache.SerializationType == SerializationType.MessagePack)
            {
                var bytes = cacheEntry.ToMessagePack();
                _ = await remoteCache.SetAsync(key, bytes, slidingExpiration, absoluteExpiration, flags: flags);
                await InvalidateLocalCache(key);
            }
            else
                throw new NotSupportedException($"{nameof(_cachingConfig.RemoteCache.SerializationType)} {_cachingConfig.RemoteCache.SerializationType} is not supported!");
        }

        localCache.Set(key, cacheEntry, slidingExpiration);
    }

    /// <inheritdoc/>
    public async Task<bool> Delete(string key, CommandFlags flags = CommandFlags.FireAndForget)
    {
        var result1 = localCache.Delete(key);
        var result2 = false;
        if (_cachingConfig.RemoteCache.IsEnabled)
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
        if (_cachingConfig.RemoteCache.IsEnabled && _cachingConfig.LocalCacheInvalidationEnabled)
        {
            _ = await remoteCache.Subscriber.PublishAsync(RedisChannel.Literal(nameof(LocalCacheExpiryService)),
                $"{_cachingConfig.PubSubPrefix}:{key}", flags);
            logger.LogTrace("{ClassName} sent {AbstractionName} expiration message for {Key} via pub/sub",
                nameof(DistributedCacheService), nameof(ILocalCache), key);
        }
    }

    /// <inheritdoc/>
    public async Task<long> DeleteAll(CommandFlags flags = CommandFlags.None, CancellationToken cancellationToken = default)
    {
        var localCount = localCache.DeleteAll();
        long remoteCount = 0;
        if (_cachingConfig.RemoteCache.IsEnabled)
        {
            var server = remoteCache.Server;
            const int batchSize = 1000;
            var batch = new List<RedisKey>(batchSize);

            foreach (var key in server.Keys(_cachingConfig.RemoteCache.DatabaseId, pageSize: batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                batch.Add(key);

                if (batch.Count >= batchSize)
                {
                    remoteCount += await remoteCache.Db.KeyDeleteAsync(batch.ToArray(), flags).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                remoteCount += await remoteCache.Db.KeyDeleteAsync(batch.ToArray(), flags).ConfigureAwait(false);
            }
        }
        return localCount + remoteCount;
    }
}
