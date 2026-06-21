namespace CasCap.Common.Services;

/// <summary>
/// When a change to a cached item is effected by the <see cref="IDistributedCache"/> this service comes into action.
/// <inheritdoc cref="DistributedCacheService.InvalidateLocalCache(string, CommandFlags)"/>
/// </summary>
public sealed class LocalCacheExpiryService(ILogger<LocalCacheExpiryService> logger, IOptions<CachingConfig> cachingConfig,
    IRemoteCache remoteCache, ILocalCache localCache)
{
    /// <summary>
    /// Subscribes to Redis pub/sub and runs until cancellation, processing local cache invalidation messages.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!cachingConfig.Value.LocalCacheInvalidationEnabled) return;

        logger.LogInformation("{ClassName} starting", nameof(LocalCacheExpiryService));

        var channelName = nameof(LocalCacheExpiryService);
        var channel = RedisChannel.Pattern(channelName);
        logger.LogDebug("{ClassName} subscribing to {ObjectType} name {ChannelName}, {PropertyName}={IsPattern}",
            nameof(LocalCacheExpiryService), typeof(RedisChannel), channelName, nameof(RedisChannel.IsPattern), channel.IsPattern);
        // Synchronous handler
        remoteCache.Subscriber.Subscribe(channel).OnMessage(channelMessage =>
        {
            var key = (string?)channelMessage.Message;
            if (key is not null)
                ExpireByKey(key);
        });

        //keep alive until cancellation is requested; the expected cancellation must not surface as an exception so the
        //unsubscribe/housekeeping below still runs on a graceful shutdown.
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        logger.LogDebug("{ClassName} unsubscribing from {ObjectType} name {ChannelName}, {PropertyName}={IsPattern}",
            nameof(LocalCacheExpiryService), typeof(RedisChannel), channelName, nameof(RedisChannel.IsPattern), channel.IsPattern);
        await remoteCache.Subscriber.UnsubscribeAsync(channel).ConfigureAwait(false);

        logger.LogInformation("{ClassName} stopping", nameof(LocalCacheExpiryService));
    }

    /// <summary>
    /// We expire any cache items from <see cref="ILocalCache"/> when their PubSubPrefix matches the incoming key.
    /// </summary>
    private void ExpireByKey(string clientNamePrefixedKey)
    {
        var parts = clientNamePrefixedKey.Split([':'], 2);
        var clientName = parts[0];
        var key = parts[1];
        if (!clientName.Equals(cachingConfig.Value.PubSubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (localCache.Delete(key))
                logger.LogDebug("{ClassName} cache key {Key} was invalidated by client {ClientName} now removed from {AbstractionName}",
                    nameof(LocalCacheExpiryService), key, clientName, nameof(ILocalCache));
        }
        else
            logger.LogTrace("{ClassName} skipped removing {Key} from {AbstractionName} as this instance just raised that event",
                nameof(LocalCacheExpiryService), key, nameof(ILocalCache));
    }
}
