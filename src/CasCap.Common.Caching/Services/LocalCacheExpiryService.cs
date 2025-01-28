namespace CasCap.Services;

/// <summary>
/// This local cache invalidation service is limited in it's scope.
/// Only cache keys that are directly Set or Deleted by this library will automatically be removed from local cache.
/// </summary>
/// <remarks>
/// Subscribe to the full cache key events and expire via a more advanced configuration model.
/// </remarks>
public class LocalCacheExpiryService(ILogger<LocalCacheExpiryService> logger,
    IRemoteCache remoteCache, ILocalCache localCache, IOptions<CachingOptions> cachingOptions)
{
    private long count = 0;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!cachingOptions.Value.LocalCacheInvalidationEnabled) return;

        logger.LogInformation("{className} starting", nameof(LocalCacheExpiryService));

        var channel = RedisChannel.Pattern(cachingOptions.Value.ChannelName);

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
        //    logger.LogInformation("{className} type={type}, key={key}",
        //        nameof(LocalCacheInvalidationBgService), redisValue, key);
        //});

        //keep alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }

        logger.LogInformation("{className} unsubscribing from remote cache subscription channel {channelName}",
            nameof(LocalCacheExpiryService), cachingOptions.Value.ChannelName);
        await remoteCache.Subscriber.UnsubscribeAsync(channel);

        //static string GetKey(string channel)
        //{
        //    var index = channel.IndexOf(':');
        //    if (index >= 0 && index < channel.Length - 1)
        //        return channel[(index + 1)..];
        //    return channel;
        //}
        logger.LogInformation("{className} stopping", nameof(LocalCacheExpiryService));
    }

    private void ExpireByKey(string clientNamePrefixedKey)
    {
        var firstIndex = clientNamePrefixedKey.IndexOf(':');
        if (firstIndex < 1) return;
        var clientName = clientNamePrefixedKey.Substring(0, firstIndex);
        var key = clientNamePrefixedKey.Substring(firstIndex + 1);
        if (!clientName.Equals(cachingOptions.Value.PubSubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (localCache.Delete(key))
                logger.LogDebug("{className} cache key {key} was invalidated by client {clientName} now removed from local cache",
                    nameof(LocalCacheExpiryService), key, clientName);
            _ = Interlocked.Increment(ref count);
        }
        else
            logger.LogTrace("{className} skipped removing {key} from local cache as this instance just added it",
                nameof(LocalCacheExpiryService), key);
    }
}
