namespace CasCap.Services;

public class LocalCacheInvalidationBgService(ILogger<LocalCacheInvalidationBgService> logger,
    IRemoteCache remoteCache, ILocalCache localCache, IOptions<CachingOptions> cachingOptions) : BackgroundService
{
    readonly CachingOptions _cachingOptions = cachingOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_cachingOptions.LocalCacheInvalidationEnabled) return;

        logger.LogInformation("{className} starting", nameof(LocalCacheInvalidationBgService));
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        //catch (Exception ex) when (ex is not OperationCanceledException) //not working, why?
        //catch (Exception ex) when (!(ex is OperationCanceledException)) //not working, why?
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error");
            throw;
        }
        logger.LogInformation("{className} stopping", nameof(LocalCacheInvalidationBgService));
    }

    long count = 0;

    async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        var channel = RedisChannel.Pattern(_cachingOptions.ChannelName);

        //// Synchronous handler
        //remoteCache.Subscriber.Subscribe(channel).OnMessage(channelMessage =>
        //{
        //    var key = (string?)channelMessage.Message;
        //    if (key is not null)
        //        localCache.Delete(key);
        //});

        // Asynchronous handler
        remoteCache.Subscriber.Subscribe(channel).OnMessage(async channelMessage =>
        {
            var key = (string?)channelMessage.Message;
            if (key is not null)
                await ExpireByKey(key, cancellationToken);
        });

        //keep alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }

        logger.LogInformation("{className} unsubscribing from remote cache channel {channelName}",
            nameof(LocalCacheInvalidationBgService), _cachingOptions.ChannelName);
        await remoteCache.Subscriber.UnsubscribeAsync(channel);
    }

    async Task ExpireByKey(string key, CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        var firstIndex = key.IndexOf(':');
        var keyPrefix = key.Substring(0, firstIndex);
        var keySuffix = key.Substring(firstIndex);
        if (!keyPrefix.Equals(_cachingOptions.PubSubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (localCache.Delete(keySuffix))
                logger.LogTrace("{className} removed {key} from local cache", nameof(LocalCacheInvalidationBgService), keySuffix);
            _ = Interlocked.Increment(ref count);
        }
    }
}
