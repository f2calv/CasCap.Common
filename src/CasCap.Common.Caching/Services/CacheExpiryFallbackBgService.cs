using Microsoft.Extensions.DependencyInjection;

namespace CasCap.Common.Services;

/// <summary>
/// Runs <see cref="CacheExpiryBgService"/> when no <see cref="IBgFeatureHost"/> is registered to launch it.
/// </summary>
/// <remarks>
/// Keeps local and remote cache expiry running for hosts that call <c>AddCasCapCaching</c> without the
/// string-based feature host, without making the caching package depend on it.
/// </remarks>
public sealed class CacheExpiryFallbackBgService(ILogger<CacheExpiryFallbackBgService> logger,
    IServiceProvider serviceProvider, CacheExpiryBgService cacheExpiryBgService) : BackgroundService
{
    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hosted services are resolved lazily because injecting them would create a construction cycle.
        var featureHostRegistered = serviceProvider.GetServices<IHostedService>().OfType<IBgFeatureHost>().Any();
        if (featureHostRegistered)
            return Task.CompletedTask;
        CacheLog.Lifecycle(logger, nameof(CacheExpiryFallbackBgService), "no feature host registered, running cache expiry directly");
        return cacheExpiryBgService.ExecuteAsync(stoppingToken);
    }
}
