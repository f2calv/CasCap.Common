namespace CasCap.Common.Services;

/// <summary>
/// Background service that runs <see cref="LocalCacheExpiryService"/> and <see cref="RemoteCacheExpiryService"/> concurrently.
/// </summary>
public sealed class CacheExpiryBgService(ILogger<CacheExpiryBgService> logger,
    LocalCacheExpiryService localCacheExpirySvc, RemoteCacheExpiryService remoteCacheExpirySvc) : BackgroundService
{
    /// <inheritdoc/>
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("{ClassName} starting", nameof(CacheExpiryBgService));
        // await-await-WhenAny propagates the first faulted task immediately so the
        // service crashes and the pod restarts rather than running in a degraded state.
        await await Task.WhenAny(
            localCacheExpirySvc.ExecuteAsync(stoppingToken),
            remoteCacheExpirySvc.ExecuteAsync(stoppingToken)).ConfigureAwait(false);
        logger.LogInformation("{ClassName} exiting", nameof(CacheExpiryBgService));
    }
}
