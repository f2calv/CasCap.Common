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
        services.AddSingleton<IConfigureOptions<CachingOptions>>(s =>
        {
            var configuration = s.GetService<IConfiguration?>();
            return new ConfigureOptions<CachingOptions>(options => configuration?.Bind(CachingOptions.SectionKey, options));
        });
        AsyncKeyedLockPoolingOptions asyncKeyedLockPoolingOptions = new();
        services.AddSingleton<IConfigureOptions<AsyncKeyedLockPoolingOptions>>(s =>
        {
            var configuration = s.GetService<IConfiguration?>();
            return new ConfigureOptions<AsyncKeyedLockPoolingOptions>(options =>
            {
                configuration?.Bind(AsyncKeyedLockPoolingOptions.SectionKey, options);
                asyncKeyedLockPoolingOptions = options;
            });
        });
        services.AddSingleton(new AsyncKeyedLocker<string>(o =>
        {
            o.PoolSize = asyncKeyedLockPoolingOptions.PoolSize;
            o.PoolInitialFill = asyncKeyedLockPoolingOptions.PoolInitialFill;
        }));
    }
}