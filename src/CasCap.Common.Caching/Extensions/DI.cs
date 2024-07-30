using AsyncKeyedLock;
namespace Microsoft.Extensions.DependencyInjection;

public static class DI
{
    public static void AddCasCapCaching(this IServiceCollection services, CachingOptions? cachingOptions = null)
    {
        //services.AddMemoryCache();//now added inside DistCacheService
        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        services.AddSingleton<IDistCacheService, DistCacheService>();
        services.AddSingleton<AsyncKeyedLocker<string>>();
        services.AddHostedService<LocalCacheInvalidationBgService>();
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
    }

    public static void AddCasCapCaching(this IServiceCollection services, string connectionString)
    {
        services.AddCasCapCaching(new CachingOptions { redisConnectionString = connectionString });
    }
}
