namespace CasCap.Common.Services;

/// <summary>
/// Background service that runs <see cref="LocalCacheExpiryService"/> and <see cref="RemoteCacheExpiryService"/> concurrently.
/// </summary>
public sealed class CacheExpiryBgService(ILogger<CacheExpiryBgService> logger, IOptions<CachingConfig> cachingConfig,
    LocalCacheExpiryService localCacheExpirySvc, RemoteCacheExpiryService remoteCacheExpirySvc) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var config = cachingConfig.Value;
        //only start the sub-services which are enabled, otherwise a disabled sub-service completes
        //immediately and the WhenAny below would tear this service down while the other still runs.
        var tasks = new List<Task>(2);
        if (config.LocalCacheExpiryServiceEnabled)
            tasks.Add(localCacheExpirySvc.ExecuteAsync(stoppingToken));
        if (config.RemoteCacheExpiryServiceEnabled)
            tasks.Add(remoteCacheExpirySvc.ExecuteAsync(stoppingToken));
        if (tasks.Count == 0)
        {
            CacheLog.Lifecycle(logger, nameof(CacheExpiryBgService), "exiting immediately, all expiry sub-services are disabled");
            return;
        }
        CacheLog.Lifecycle(logger, nameof(CacheExpiryBgService), "starting");
        // await-await-WhenAny propagates the first faulted task immediately so the
        // service crashes and the pod restarts rather than running in a degraded state.
        await await Task.WhenAny(tasks).ConfigureAwait(false);
        CacheLog.Lifecycle(logger, nameof(CacheExpiryBgService), "exiting");
    }
}
