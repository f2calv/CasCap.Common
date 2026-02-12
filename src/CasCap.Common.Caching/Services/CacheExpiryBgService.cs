namespace CasCap.Common.Services;

public class CacheExpiryBgService(ILogger<CacheExpiryBgService> logger,
    LocalCacheExpiryService localCacheExpirySvc, RemoteCacheExpiryService remoteCacheExpirySvc) : BackgroundService
{
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("{ClassName} starting", nameof(CacheExpiryBgService));
        await Task.WhenAll(
            localCacheExpirySvc.ExecuteAsync(stoppingToken),
            remoteCacheExpirySvc.ExecuteAsync(stoppingToken));
        logger.LogInformation("{ClassName} exiting", nameof(CacheExpiryBgService));
    }
}
