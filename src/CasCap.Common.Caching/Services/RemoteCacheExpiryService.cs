namespace CasCap.Common.Services;

/// <summary>
/// This <see cref="RemoteCacheExpiryService"/> subscribes to '__keyspace@0__:expired' events and performs
/// housekeeping activities such as removing expired items from the <see cref="IRemoteCache.SlidingExpirations"/>
/// collection.
/// </summary>
public sealed class RemoteCacheExpiryService(ILogger<RemoteCacheExpiryService> logger, IOptions<CachingConfig> cachingConfig, IRemoteCache remoteCache)
{
    /// <summary>
    /// Subscribes to Redis key expiration events and runs until cancellation, performing sliding expiration housekeeping.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        CacheLog.Lifecycle(logger, nameof(RemoteCacheExpiryService), "starting");

        var channelName = $"__keyevent@{cachingConfig.Value.RemoteCache.DatabaseId}__:expired";
        var channel = RedisChannel.Literal(channelName);
        CacheLog.ChannelSubscription(
            logger,
            nameof(RemoteCacheExpiryService),
            "subscribing to",
            typeof(RedisChannel),
            channelName,
            nameof(RedisChannel.IsPattern),
            channel.IsPattern);
        await remoteCache.Subscriber.SubscribeAsync(channel, (redisChannel, redisValue) =>
        {
            var key = redisValue.ToString();
            //lets do housekeeping
            var success = remoteCache.SlidingExpirations.TryRemove(redisValue.ToString(), out var _);
            CacheLog.ExpirationDetected(logger, nameof(RemoteCacheExpiryService), key, success, remoteCache.SlidingExpirations.Count);
        }).ConfigureAwait(false);

        //keep alive until cancellation is requested; the expected cancellation must not surface as an exception so the
        //unsubscribe/housekeeping below still runs on a graceful shutdown.
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        CacheLog.ChannelSubscription(
            logger,
            nameof(RemoteCacheExpiryService),
            "unsubscribing from",
            typeof(RedisChannel),
            channelName,
            nameof(RedisChannel.IsPattern),
            channel.IsPattern);
        await remoteCache.Subscriber.UnsubscribeAsync(channel).ConfigureAwait(false);

        CacheLog.Lifecycle(logger, nameof(RemoteCacheExpiryService), "stopping");
    }
}
