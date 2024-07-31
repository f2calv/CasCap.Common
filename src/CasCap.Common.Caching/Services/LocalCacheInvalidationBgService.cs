using StackExchange.Redis;

namespace CasCap.Services;

public class LocalCacheInvalidationBgService : BackgroundService
{
    readonly ILogger<LocalCacheInvalidationBgService> _logger;
    readonly IRemoteCacheService _remoteCacheSvc;
    readonly ILocalCacheService _localCacheSvc;
    readonly CachingOptions _cachingOptions;

    public LocalCacheInvalidationBgService(ILogger<LocalCacheInvalidationBgService> logger,
        IRemoteCacheService remoteCacheSvc, ILocalCacheService localCacheSvc, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _remoteCacheSvc = remoteCacheSvc;
        _localCacheSvc = localCacheSvc;
        _cachingOptions = cachingOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{serviceName} starting", nameof(LocalCacheInvalidationBgService));
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        //catch (Exception ex) when (ex is not OperationCanceledException) //not working, why?
        //catch (Exception ex) when (!(ex is OperationCanceledException)) //not working, why?
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error");
            throw;
        }
        _logger.LogInformation("{serviceName} exiting", nameof(LocalCacheInvalidationBgService));
    }

    async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);

        var count = 0L;
        // Synchronous handler
        //_redisCacheSvc.subscriber.Subscribe(_cachingOptions.ChannelName).OnMessage(channelMessage =>
        //{
        //    var key = (string)channelMessage.Message;
        //    _localCacheSvc.DeleteLocal(key, true);
        //});

        // Asynchronous handler
        _remoteCacheSvc.subscriber.Subscribe(RedisChannel.Literal(_cachingOptions.ChannelName)).OnMessage(async channelMessage =>
        {
            await Task.Delay(0, cancellationToken);
            var key = (string?)channelMessage.Message;
            if (key is not null && !key.StartsWith(_cachingOptions.pubSubPrefix))
            {
                var finalIndex = key.Split('_')[2];
                _localCacheSvc.DeleteLocal(finalIndex, true);
                _ = Interlocked.Increment(ref count);
            }
        });

        while (!cancellationToken.IsCancellationRequested)
            await Task.Delay(2_500, cancellationToken);
        _logger.LogInformation("{serviceName} unsubscribing from redis {channelName}",
            nameof(LocalCacheInvalidationBgService), _cachingOptions.ChannelName);
        await _remoteCacheSvc.subscriber.UnsubscribeAsync(RedisChannel.Literal(_cachingOptions.ChannelName));
    }
}
