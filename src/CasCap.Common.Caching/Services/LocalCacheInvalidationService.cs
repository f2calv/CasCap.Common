namespace CasCap.Services;

public class LocalCacheInvalidationService : BackgroundService
{
    readonly ILogger<LocalCacheInvalidationService> _logger;
    readonly IRedisCacheService _redisCacheSvc;
    readonly IDistCacheService _distCacheSvc;
    readonly CachingOptions _cachingOptions;

    public LocalCacheInvalidationService(ILogger<LocalCacheInvalidationService> logger,
        IRedisCacheService redisCacheSvc, IDistCacheService distCacheSvc, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _redisCacheSvc = redisCacheSvc;
        _distCacheSvc = distCacheSvc;
        _cachingOptions = cachingOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{type}: {serviceName} starting", nameof(BackgroundService), nameof(LocalCacheInvalidationService));
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
        _logger.LogInformation("{type}: {serviceName} exiting", nameof(BackgroundService), nameof(LocalCacheInvalidationService));
    }

    async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);

        var count = 0L;
        // Synchronous handler
        //_redisCacheSvc.subscriber.Subscribe(_cachingOptions.ChannelName).OnMessage(channelMessage =>
        //{
        //    var key = (string)channelMessage.Message;
        //    _distCacheSvc.DeleteLocal(key, true);
        //});

        // Asynchronous handler
        _redisCacheSvc.subscriber.Subscribe(_cachingOptions.ChannelName).OnMessage(async channelMessage =>
        {
            await Task.Delay(0, cancellationToken);
            var key = (string?)channelMessage.Message;
            if (key is not null && !key.StartsWith(_cachingOptions.pubSubPrefix))
            {
                var finalIndex = key.Split('_')[2];
                _distCacheSvc.DeleteLocal(finalIndex, true);
                _ = Interlocked.Increment(ref count);
            }
        });

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Unsubscribing from redis {channelName}", _cachingOptions.ChannelName);
                _redisCacheSvc.subscriber.Unsubscribe(_cachingOptions.ChannelName);
                break;
            }
            await Task.Delay(2_500, cancellationToken);
        }
    }
}