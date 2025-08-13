namespace CasCap.Services;

/// <summary>
/// When a change to a cached item is effected by the <see cref="IDistributedCache"/> this service comes into action.
/// <inheritdoc cref="DistributedCacheService.InvalidateLocalCache(string, CommandFlags)"/>
/// </summary>
public class LocalCacheExpiryService(ILogger<LocalCacheExpiryService> logger,
    IRemoteCache remoteCache, ILocalCache localCache, IOptions<CachingOptions> cachingOptions)
{
    private long count = 0;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!cachingOptions.Value.LocalCacheInvalidationEnabled) return;

        logger.LogInformation("{ClassName} starting", nameof(LocalCacheExpiryService));

        var channelName = nameof(LocalCacheExpiryService);
        var channel = RedisChannel.Pattern(channelName);
        logger.LogDebug("{ClassName} subscribing to {objectType} name {channelName}, {propertyName}={IsPattern}",
            nameof(LocalCacheExpiryService), typeof(RedisChannel), channelName, nameof(RedisChannel.IsPattern), channel.IsPattern);
        // Synchronous handler
        remoteCache.Subscriber.Subscribe(channel).OnMessage(channelMessage =>
        {
            var key = (string?)channelMessage.Message;
            if (key is not null)
                ExpireByKey(key);
        });

        //// Asynchronous handler
        //remoteCache.Subscriber.Subscribe(channel).OnMessage(async channelMessage =>
        //{
        //    var key = (string?)channelMessage.Message;
        //    if (key is not null)
        //        await ExpireByKey(key, cancellationToken);
        //});

        //await remoteCache.Subscriber.SubscribeAsync(channel, (redisChannel, redisValue) =>
        //{
        //    var key = GetKey(redisChannel);
        //    switch (redisValue)
        //    {
        //        case "del":
        //        case "set":
        //        //case "expire":
        //        //case "expired":
        //            ExpireByKey(key);
        //            break;
        //    }
        //    logger.LogInformation("{ClassName} type={type}, key={key}",
        //        nameof(LocalCacheInvalidationBgService), redisValue, key);
        //});

        //keep alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }

        logger.LogDebug("{ClassName} unsubscribing from {objectType} name {channelName}, {propertyName}={IsPattern}",
            nameof(LocalCacheExpiryService), typeof(RedisChannel), channelName, nameof(RedisChannel.IsPattern), channel.IsPattern);
        await remoteCache.Subscriber.UnsubscribeAsync(channel);

        //static string GetKey(string channel)
        //{
        //    var index = channel.IndexOf(':');
        //    if (index >= 0 && index < channel.Length - 1)
        //        return channel[(index + 1)..];
        //    return channel;
        //}
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
        if (!clientName.Equals(cachingOptions.Value.PubSubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (localCache.Delete(key))
                logger.LogDebug("{ClassName} cache key {key} was invalidated by client {clientName} now removed from {abstractionName}",
                    nameof(LocalCacheExpiryService), key, clientName, nameof(ILocalCache));
            _ = Interlocked.Increment(ref count);
        }
        else
            logger.LogTrace("{ClassName} skipped removing {key} from {abstractionName} as this instance just raised that event",
                nameof(LocalCacheExpiryService), key, nameof(ILocalCache));
    }
}
