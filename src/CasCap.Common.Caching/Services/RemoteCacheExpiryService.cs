namespace CasCap.Services;

/// <summary>
/// This <see cref="RemoteCacheExpiryService"/> subscribes to '__keyspace@0__:expired' events and performs
/// housekeeping activities such as removing expired items from the <see cref="IRemoteCache.SlidingExpirations"/>
/// collection.
/// </summary>
public class RemoteCacheExpiryService(ILogger<RemoteCacheExpiryService> logger, IRemoteCache remoteCache, IOptions<CachingOptions> cachingOptions)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{className} starting", nameof(RemoteCacheExpiryService));

        var channelName = $"__keyevent@{cachingOptions.Value.RemoteCache.DatabaseId}__:expired";
        var channel = RedisChannel.Literal(channelName);
        logger.LogDebug("{className} subscribing to {objectType} name {channelName}, {propertyName}={IsPattern}",
            nameof(RemoteCacheExpiryService), typeof(RedisChannel), channelName, nameof(RedisChannel.IsPattern), channel.IsPattern);
        await remoteCache.Subscriber.SubscribeAsync(channel, (redisChannel, redisValue) =>
        {
            var key = redisValue.ToString();
            //lets do housekeeping
            var success = remoteCache.SlidingExpirations.TryRemove(redisValue.ToString(), out var _);
            logger.LogTrace("{className} expiration detected key={key}, removal status={success}, {count} item(s) remaining",
                nameof(RemoteCacheExpiryService), key, success, remoteCache.SlidingExpirations.Count);
        });

        //keep alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }

        logger.LogDebug("{className} unsubscribing from {objectType} name {channelName}, {propertyName}={IsPattern}",
            nameof(RemoteCacheExpiryService), typeof(RedisChannel), channelName, nameof(RedisChannel.IsPattern), channel.IsPattern);
        await remoteCache.Subscriber.UnsubscribeAsync(channel);

        logger.LogInformation("{className} stopping", nameof(RemoteCacheExpiryService));
    }
}
