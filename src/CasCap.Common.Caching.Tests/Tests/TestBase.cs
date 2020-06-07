using CasCap.Common.Logging;
using CasCap.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace CasCap.Common.Caching.Tests
{
    public abstract class TestBase
    {
        protected IDistCacheService _distCacheSvc;
        protected IRedisCacheService _redisSvc;

        public TestBase()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.Test.json", optional: false, reloadOnChange: false)
                .Build();

            //initiate ServiceCollection w/logging
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddLogging(logging =>
                {
                    logging.AddDebug();
                    ApplicationLogging.LoggerFactory = logging.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
                });

            //add services
            services.AddCasCapCaching();

            var serviceProvider = services.BuildServiceProvider();
            _distCacheSvc = serviceProvider.GetRequiredService<IDistCacheService>();
            _redisSvc = serviceProvider.GetRequiredService<IRedisCacheService>();
        }
    }
}