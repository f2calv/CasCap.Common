﻿using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Threading;

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
        if (!_cachingOptions.LocalCacheInvalidationEnabled) return;

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
        _logger.LogInformation("{serviceName} stopping", nameof(LocalCacheInvalidationBgService));
    }

    long count = 0;

    async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        var channel = RedisChannel.Literal(_cachingOptions.ChannelName);

        //// Synchronous handler
        //_remoteCacheSvc.subscriber.Subscribe(channel).OnMessage(channelMessage =>
        //{
        //    var key = (string)channelMessage.Message;
        //    _localCacheSvc.DeleteLocal(key, true);
        //});

        // Asynchronous handler
        _remoteCacheSvc.Subscriber.Subscribe(channel).OnMessage(async channelMessage =>
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

        _logger.LogInformation("{serviceName} unsubscribing from remote cache channel {channelName}",
            nameof(LocalCacheInvalidationBgService), _cachingOptions.ChannelName);
        await _remoteCacheSvc.Subscriber.UnsubscribeAsync(channel);
    }

    async Task ExpireByKey(string key, CancellationToken cancellationToken)
    {
        await Task.Delay(0, cancellationToken);
        var firstIndex = key.IndexOf(':');
        var keyPrefix = key.Substring(0, firstIndex);
        var keySuffix = key.Substring(firstIndex);
        if (!keyPrefix.Equals(_cachingOptions.pubSubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (_localCacheSvc.Delete(keySuffix))
                _logger.LogTrace("{serviceName} removed {key} from local cache", nameof(LocalCacheInvalidationBgService), keySuffix);
            _ = Interlocked.Increment(ref count);
        }
    }
}
