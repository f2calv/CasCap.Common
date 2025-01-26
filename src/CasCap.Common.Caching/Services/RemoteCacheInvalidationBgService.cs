namespace CasCap.Services;

/// <summary>
/// This remote cache invalidation service subscribes to expire events and removes them from .
/// Only cache keys that are directly Set or Deleted by this library will automatically be removed from local cache.
/// </summary>
public class RemoteCacheInvalidationBgService(ILogger<RemoteCacheInvalidationBgService> logger,
    IRemoteCache remoteCache, IOptions<CachingOptions> cachingOptions) : BackgroundService
{
    private readonly CachingOptions _cachingOptions = cachingOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{className} starting", nameof(RemoteCacheInvalidationBgService));
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        //catch (Exception ex) when (ex is not OperationCanceledException) //not working, why?
        //catch (Exception ex) when (!(ex is OperationCanceledException)) //not working, why?
        catch (Exception ex)
        {
            logger.LogCritical(ex, "{className} fatal error", nameof(RemoteCacheInvalidationBgService));
            throw;
        }
        logger.LogInformation("{className} stopping", nameof(RemoteCacheInvalidationBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        //__keyspace@0__:*
        var channel = RedisChannel.Literal("__keyevent@0__:expired");

        await remoteCache.Subscriber.SubscribeAsync(channel, (redisChannel, redisValue) =>
        {
            var key = redisValue.ToString();
            //do housekeeping
            var success = remoteCache.SlidingExpirations.TryRemove(redisValue.ToString(), out var _);
            logger.LogTrace("{className} expiration detected key={key}, dictionary={count}, success={success}",
                nameof(RemoteCacheInvalidationBgService), key, remoteCache.SlidingExpirations.Count, success);
        });

        //keep alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }

        logger.LogInformation("{className} unsubscribing from remote cache subscription channel {channelName}",
            nameof(RemoteCacheInvalidationBgService), _cachingOptions.ChannelName);
        await remoteCache.Subscriber.UnsubscribeAsync(channel);
    }
}
