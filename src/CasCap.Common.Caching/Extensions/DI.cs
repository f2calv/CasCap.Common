using StackExchange.Redis;
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to setup local/remote/distributed Caching services.
/// Follows official best practice/guidance from Microsoft for library authors, https://learn.microsoft.com/en-us/dotnet/core/extensions/options-library-authors
/// </summary>
public static class DI
{
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, IConfiguration configuration,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(configuration: configuration, remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, CachingOptions cachingOptions,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(cachingOptions: cachingOptions, remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, Action<CachingOptions> configureOptions,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(configureOptions: configureOptions, remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    static ConnectionMultiplexer? AddServices(this IServiceCollection services,
        IConfiguration? configuration = null,
        CachingOptions? cachingOptions = null,
        Action<CachingOptions>? configureOptions = null,
        string? remoteCacheConnectionString = null,
        CacheType LocalCacheType = CacheType.Memory,
        CacheType RemoteCacheType = CacheType.Redis
        )
    {
        if (configuration is not null)
        {
            var configSection = configuration.GetSection(CachingOptions.SectionKey);
            cachingOptions = configSection.Get<CachingOptions>();
            if (cachingOptions is not null)
                services.Configure<CachingOptions>(configSection);
        }
        else if (cachingOptions is not null)
        {
            var options = Options.Options.Create(cachingOptions);
            services.AddSingleton(options);
            //services.AddOptions<CachingOptions>()
            //    .Configure(options =>
            //    {
            //        //options = cachingOptions;//this won't work
            //        options.LoadBuiltInLuaScripts = cachingOptions.LoadBuiltInLuaScripts;
            //        // Overwrite default option values with the user provided options.
            //        // options.ChannelName = cachingOptions.ChannelName;
            //    });
        }
        else if (configureOptions is not null)
        {
            services.Configure(configureOptions);
            cachingOptions = new();
            configureOptions.Invoke(cachingOptions);
        }

        //services.AddSingleton<IConfigureOptions<CachingOptions>>(s =>
        //{
        //    var configuration = s.GetService<IConfiguration?>();
        //    return new ConfigureOptions<CachingOptions>(options => configuration?.Bind(CachingOptions.SectionKey, options));
        //});
        //services.AddOptions<CachingOptions>()
        //    .Configure<IConfiguration>((options, configuration) => configuration.GetSection(CachingOptions.SectionKey).Bind(options));

        if (LocalCacheType == CacheType.Memory)
        {
            //services.AddMemoryCache();//now added via MemoryCacheService
            services.AddSingleton<ILocalCacheService, MemoryCacheService>();
        }
        else if (LocalCacheType == CacheType.Disk)
            services.AddSingleton<ILocalCacheService, DiskCacheService>();
        else
            throw new NotSupportedException($"{nameof(LocalCacheType)} {LocalCacheType} is not supported!");

        if (!string.IsNullOrWhiteSpace(remoteCacheConnectionString))
        {
            if (RemoteCacheType == CacheType.Redis)
                services.AddSingleton<IRemoteCacheService, RedisCacheService>();
            else
                throw new NotSupportedException($"{nameof(RemoteCacheType)} {RemoteCacheType} is not supported!");
            services.AddSingleton<IDistributedCacheService, DistributedCacheService>();
            services.AddHostedService<LocalCacheInvalidationBgService>();

            var multiplexer = GetMultiplexer(services, remoteCacheConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            //We return the ConnectionMultiplexer so it can be reused by other services requiring a Redis connection.
            return multiplexer;
        }
        else
            return null;
    }

    static ConnectionMultiplexer GetMultiplexer(IServiceCollection services, string remoteCacheConnectionString)
    {
        var configurationOptions = ConfigurationOptions.Parse(remoteCacheConnectionString);
        configurationOptions.ClientName = $"{AppDomain.CurrentDomain.FriendlyName}-{Environment.MachineName}";
        var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);
        return multiplexer;
    }
}
