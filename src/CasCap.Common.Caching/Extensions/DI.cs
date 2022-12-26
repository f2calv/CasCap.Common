using AsyncKeyedLock;

namespace Microsoft.Extensions.DependencyInjection;

public static class DI
{
    public static void AddCasCapCaching(this IServiceCollection services)
    {
        //services.AddMemoryCache();//now added inside DistCacheService
        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        services.AddSingleton<IDistCacheService, DistCacheService>();
        services.AddHostedService<LocalCacheInvalidationService>();
        CachingOptions cachingOptions = new();
        services.AddSingleton<IConfigureOptions<CachingOptions>>(s =>
        {
            var configuration = s.GetService<IConfiguration?>();
            return new ConfigureOptions<CachingOptions>(options => {
                configuration?.Bind(CachingOptions.SectionKey, options);
                cachingOptions = options;
            });
        });
        services.AddSingleton(new AsyncKeyedLocker<string>(o =>
        {
            o.PoolSize = cachingOptions.KeyedLockPoolSize;
            o.PoolInitialFill = cachingOptions.KeyedLockPoolInitialFill;
        }));
    }
}