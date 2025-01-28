namespace CasCap.Services;

/// <summary>
/// This remote cache invalidation service subscribes to expire events and removes them from .
/// Only cache keys that are directly Set or Deleted by this library will automatically be removed from local cache.
/// </summary>
public class RemoteCacheExpiryService(ILogger<RemoteCacheExpiryService> logger,
    IRemoteCache remoteCache, IOptions<CachingOptions> cachingOptions)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{className} starting", nameof(RemoteCacheExpiryService));

        //__keyspace@0__:*
        var channel = RedisChannel.Literal("__keyevent@0__:expired");

        await remoteCache.Subscriber.SubscribeAsync(channel, (redisChannel, redisValue) =>
        {
            var key = redisValue.ToString();
            //do housekeeping
            var success = remoteCache.SlidingExpirations.TryRemove(redisValue.ToString(), out var _);
            logger.LogTrace("{className} expiration detected key={key}, dictionary={count}, success={success}",
                nameof(RemoteCacheExpiryService), key, remoteCache.SlidingExpirations.Count, success);
        });

        //keep alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }

        logger.LogInformation("{className} unsubscribing from remote cache subscription channel {channelName}",
            nameof(RemoteCacheExpiryService), cachingOptions.Value.ChannelName);
        await remoteCache.Subscriber.UnsubscribeAsync(channel);

        logger.LogInformation("{className} stopping", nameof(RemoteCacheExpiryService));
    }
}
