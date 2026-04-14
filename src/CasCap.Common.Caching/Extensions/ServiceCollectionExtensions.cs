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
    {
        services.AddOptions<CachingConfig>();
        return services.AddServices(remoteCacheConnectionString, LocalCacheType);
    }

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, IConfiguration configuration,
        string? sectionName = null,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
        sectionName ??= CachingConfig.ConfigurationSectionName;
        var section = configuration.GetSection(sectionName);
        services.AddOptionsWithValidateOnStart<CachingConfig>()
            .Bind(section)
            .ValidateDataAnnotations();
        services.AddOptions<RedlockConfig>()
            .Bind(section.GetSection(nameof(CachingConfig.Redlock)));
        var cachingConfig = section.Get<CachingConfig>() ?? new CachingConfig();
        var connStr = remoteCacheConnectionString ?? cachingConfig.RemoteCacheConnectionString;
        return services.AddServices(connStr, LocalCacheType,
            enableDistributedLocking: cachingConfig.EnableDistributedLocking,
            redisKeyFormat: cachingConfig.RedisKeyFormat);
    }

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, CachingConfig cachingConfig,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
        services.AddOptions<CachingConfig>()
            .Configure(options =>
            {
                options.MemoryCacheSizeLimit = cachingConfig.MemoryCacheSizeLimit;
                options.MemoryCacheItemPriority = cachingConfig.MemoryCacheItemPriority;
                options.UseBuiltInLuaScripts = cachingConfig.UseBuiltInLuaScripts;
                options.MemoryCache = cachingConfig.MemoryCache;
                options.DiskCache = cachingConfig.DiskCache;
                options.RemoteCache = cachingConfig.RemoteCache;
                options.LocalCacheInvalidationEnabled = cachingConfig.LocalCacheInvalidationEnabled;
                options.ExpirationSyncMode = cachingConfig.ExpirationSyncMode;
                options.EnableDistributedLocking = cachingConfig.EnableDistributedLocking;
                options.Redlock = cachingConfig.Redlock;
            });
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(cachingConfig.Redlock));
        var connStr = remoteCacheConnectionString ?? cachingConfig.RemoteCacheConnectionString;
        return services.AddServices(connStr, LocalCacheType,
            enableDistributedLocking: cachingConfig.EnableDistributedLocking,
            redisKeyFormat: cachingConfig.RedisKeyFormat);
    }

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, Action<CachingConfig> configureConfig,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
        services.Configure(configureConfig);
        return services.AddServices(remoteCacheConnectionString, LocalCacheType);
    }

    private static ConnectionMultiplexer? AddServices(this IServiceCollection services,
        string? remoteCacheConnectionString,
        CacheType LocalCacheType,
        CacheType RemoteCacheType = CacheType.Redis,
        bool enableDistributedLocking = false,
        string redisKeyFormat = "RedLock:{0}")
    {
        //ensure RedlockConfig is always available (idempotent; won't override bound config from overload #2)
        services.AddOptions<RedlockConfig>();

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
            if (RemoteCacheType != CacheType.Redis)
                throw new NotSupportedException($"{nameof(RemoteCacheType)} {RemoteCacheType} is not supported!");

            services.AddSingleton<IRemoteCache, RedisCacheService>();
            services.AddSingleton<RemoteCacheExpiryService>();
            services.AddSingleton<IDistributedCache, DistributedCacheService>();
            services.AddSingleton<LocalCacheExpiryService>();
            services.AddHostedService<CacheExpiryBgService>();

            var multiplexer = GetMultiplexer(remoteCacheConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            if (enableDistributedLocking)
            {
                var rlMuxer = (RedLockMultiplexer)multiplexer;
                rlMuxer.RedisKeyFormat = redisKeyFormat;
                var redLockFactory = RedLockFactory.Create([rlMuxer]);
                services.AddSingleton<IDistributedLockFactory>(redLockFactory);
            }

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
