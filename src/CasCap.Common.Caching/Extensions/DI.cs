using StackExchange.Redis;
namespace Microsoft.Extensions.DependencyInjection;

public static class DI
{
    public static ConnectionMultiplexer AddCasCapCaching(this IServiceCollection services, string redisConnectionString, CachingOptions? cachingOptions = null)
    {
        if (cachingOptions is null)
            services.AddSingleton<IConfigureOptions<CachingOptions>>(s =>
            {
                var configuration = s.GetService<IConfiguration?>();
                return new ConfigureOptions<CachingOptions>(options => configuration?.Bind(CachingOptions.SectionKey, options));
            });
        else
        {
            var options = Options.Options.Create(cachingOptions);
            services.AddSingleton(options);
        }

        //services.AddMemoryCache();//now added via ILocalCacheService
        //TODO: add feature flag to switch between either memory or disk local cache
        services.AddSingleton<ILocalCacheService, MemoryCacheService>();
        //services.AddSingleton<ILocalCacheService, DiskCacheService>();
        services.AddSingleton<IRemoteCacheService, RedisCacheService>();
        services.AddSingleton<IDistributedCacheService, DistributedCacheService>();
        services.AddHostedService<LocalCacheInvalidationBgService>();

        var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);
        configurationOptions.ClientName = $"{AppDomain.CurrentDomain.FriendlyName}-{Environment.MachineName}";
        var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);

        return multiplexer;
    }
}
