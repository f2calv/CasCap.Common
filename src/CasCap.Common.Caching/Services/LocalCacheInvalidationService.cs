using CasCap.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;
namespace CasCap.Services
{
    public class LocalCacheInvalidationService : BackgroundService
    {
        readonly ILogger<LocalCacheInvalidationService> _logger;
        readonly IRedisCacheService _redisCacheSvc;
        readonly IDistCacheService _distCacheSvc;
        readonly CachingConfig _cachingConfig;

        public LocalCacheInvalidationService(ILogger<LocalCacheInvalidationService> logger,
            IRedisCacheService redisCacheSvc, IDistCacheService distCacheSvc, IOptions<CachingConfig> cachingConfig)
        {
            _logger = logger;
            _redisCacheSvc = redisCacheSvc;
            _distCacheSvc = distCacheSvc;
            _cachingConfig = cachingConfig.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting {serviceName}...", nameof(LocalCacheInvalidationService));
            await Task.Delay(0);

            // Synchronous handler
            _redisCacheSvc.subscriber.Subscribe(_cachingConfig.ChannelName).OnMessage(channelMessage =>
            {
                var key = (string)channelMessage.Message;
                _distCacheSvc.DeleteLocal(key, true);
            });

            // Asynchronous handler
            //_redisCacheSvc.subscriber.Subscribe(_cachingConfig.ChannelName).OnMessage(async channelMessage =>
            //{
            //    var key = (string)channelMessage.Message;
            //    _distCacheSvc.RemoveLocal(key, true);
            //});
        }
    }
}