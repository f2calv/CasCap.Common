namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to setup local/remote/distributed Caching services.
/// Follows official best practice/guidance from Microsoft for library authors,
/// <see href="https://learn.microsoft.com/en-us/dotnet/core/extensions/options-library-authors"/>.
/// </summary>
/// <remarks>
/// Note: Official documentation says to not add these extension methods to the
/// <see cref="DependencyInjection"/> namespace however we are opting to ignore that recommendation!
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add all necessary services to enable the CasCap distributed caching API.
    /// </summary>
    /// <param name="services">The service collection to add caching services to.</param>
    /// <param name="remoteCacheConnectionString">Redis connection string. When <c>null</c>, only local caching is enabled.</param>
    /// <param name="LocalCacheType"><inheritdoc cref="CacheType" path="/summary"/></param>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, IConfiguration configuration,
        string sectionName = CachingConfig.ConfigurationSectionName,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(configuration: configuration, sectionName, remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, CachingConfig cachingConfig,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(cachingConfig: cachingConfig, remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, Action<CachingConfig> configureConfig,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
        => services.AddServices(configureOptions: configureConfig, remoteCacheConnectionString: remoteCacheConnectionString, LocalCacheType: LocalCacheType);

    private static ConnectionMultiplexer? AddServices(this IServiceCollection services,
        IConfiguration? configuration = null,
        string sectionName = CachingConfig.ConfigurationSectionName,
        CachingConfig? cachingConfig = null,
        Action<CachingConfig>? configureOptions = null,
        string? remoteCacheConnectionString = null,
        CacheType LocalCacheType = CacheType.Memory,
        CacheType RemoteCacheType = CacheType.Redis
        )
    {
        if (configuration is not null)
        {
            var configSection = configuration.GetSection(sectionName);
            cachingConfig = configSection.Get<CachingConfig>();
            if (cachingConfig is not null)
                services.Configure<CachingConfig>(configSection);
        }
        else if (cachingConfig is not null)
        {
            var options = Options.Options.Create(cachingConfig);
            services.AddSingleton(options);
            //services.AddOptions<CachingConfig>()
            //    .Configure(options =>
            //    {
            //        //options = cachingConfig;//this won't work
            //        options.LoadBuiltInLuaScripts = cachingConfig.LoadBuiltInLuaScripts;
            //        // Overwrite default option values with the user provided options.
            //        // options.ChannelName = cachingConfig.ChannelName;
            //    });
        }
        else if (configureOptions is not null)
        {
            services.Configure(configureOptions);
            cachingConfig = new();
            configureOptions.Invoke(cachingConfig);
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
            services.AddSingleton<ILocalCache, MemoryCacheService>();
        }
        else if (LocalCacheType == CacheType.Disk)
            services.AddSingleton<ILocalCache, DiskCacheService>();
        else
            throw new NotSupportedException($"{nameof(LocalCacheType)} {LocalCacheType} is not supported!");

        if (
#if NET8_0_OR_GREATER
            !string.IsNullOrWhiteSpace(remoteCacheConnectionString)
#else
            !string.IsNullOrWhiteSpace(remoteCacheConnectionString) && remoteCacheConnectionString is not null
#endif
            )
        {
            if (RemoteCacheType == CacheType.Redis)
            {
                services.AddSingleton<IRemoteCache, RedisCacheService>();
                services.AddSingleton<RemoteCacheExpiryService>();
            }
            else
                throw new NotSupportedException($"{nameof(RemoteCacheType)} {RemoteCacheType} is not supported!");
            services.AddSingleton<IDistributedCache, DistributedCacheService>();
            services.AddSingleton<LocalCacheExpiryService>();
            services.AddHostedService<CacheExpiryBgService>();

            var multiplexer = GetMultiplexer(remoteCacheConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            //We return the ConnectionMultiplexer so it can be reused by other services requiring a Redis connection.
            return multiplexer;
        }
        else
            return null;
    }

    private static ConnectionMultiplexer GetMultiplexer(string remoteCacheConnectionString)
    {
        var configurationOptions = ConfigurationOptions.Parse(remoteCacheConnectionString);
        configurationOptions.ClientName = $"{AppDomain.CurrentDomain.FriendlyName}-{Environment.MachineName}";
        var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);
        return multiplexer;
    }
}
