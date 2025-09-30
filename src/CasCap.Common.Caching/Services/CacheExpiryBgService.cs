namespace CasCap.Common.Services;

public class CacheExpiryBgService(ILogger<CacheExpiryBgService> logger,
    LocalCacheExpiryService localCacheExpirySvc, RemoteCacheExpiryService remoteCacheExpirySvc) : BackgroundService
{
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("{ClassName} starting", nameof(CacheExpiryBgService));
        var tasks = new List<Task>
        {
            localCacheExpirySvc.ExecuteAsync(stoppingToken),
            remoteCacheExpirySvc.ExecuteAsync(stoppingToken),
        };
        if (tasks.IsNullOrEmpty())
            throw new GenericException("no services found to launch!");
        await Task.WhenAll(tasks);
        logger.LogInformation("{ClassName} exiting", nameof(CacheExpiryBgService));
    }
}
