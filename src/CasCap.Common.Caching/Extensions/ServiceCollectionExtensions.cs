#if NET8_0_OR_GREATER
using HealthChecks.Redis;
#endif
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <summary>Add all necessary services to enable the CasCap distributed caching API.</summary>
    /// <param name="services">The service collection to add caching services to.</param>
    /// <param name="remoteCacheConnectionString">Redis connection string. When <c>null</c>, only local caching is enabled.</param>
    /// <param name="LocalCacheType"><inheritdoc cref="CacheType" path="/summary"/></param>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
        services.AddOptionsWithValidateOnStart<CachingConfig>()
            .ValidateDataAnnotations();
        return services.AddServices(remoteCacheConnectionString, LocalCacheType);
    }

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, IConfiguration configuration,
        string? sectionName = null,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));

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
            distributedLockingEnabled: cachingConfig.DistributedLockingEnabled,
            redisKeyFormat: cachingConfig.Redlock.RedisKeyFormat,
            redisDatabaseId: cachingConfig.RemoteCache.DatabaseId,
            healthCheckRedis: cachingConfig.HealthCheckRedis,
            cacheExpiryServiceEnabled: cachingConfig.CacheExpiryServiceEnabled);
    }

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, CachingConfig cachingConfig,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
        if (cachingConfig is null) throw new ArgumentNullException(nameof(cachingConfig));

        services.AddOptionsWithValidateOnStart<CachingConfig>()
            .Configure(options =>
            {
                options.MemoryCacheSizeLimit = cachingConfig.MemoryCacheSizeLimit;
                options.MemoryCacheItemPriority = cachingConfig.MemoryCacheItemPriority;
                options.UseBuiltInLuaScripts = cachingConfig.UseBuiltInLuaScripts;
                options.MemoryCache = cachingConfig.MemoryCache;
                options.DiskCache = cachingConfig.DiskCache;
                options.RemoteCache = cachingConfig.RemoteCache;
                options.LocalCacheInvalidationEnabled = cachingConfig.LocalCacheInvalidationEnabled;
                options.CacheExpiryServiceEnabled = cachingConfig.CacheExpiryServiceEnabled;
                options.LocalCacheExpiryServiceEnabled = cachingConfig.LocalCacheExpiryServiceEnabled;
                options.RemoteCacheExpiryServiceEnabled = cachingConfig.RemoteCacheExpiryServiceEnabled;
                options.ExpirationSyncMode = cachingConfig.ExpirationSyncMode;
                options.DistributedLockingEnabled = cachingConfig.DistributedLockingEnabled;
                options.CacheKeyFormat = cachingConfig.CacheKeyFormat;
                options.Redlock = cachingConfig.Redlock;
            })
            .ValidateDataAnnotations();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(cachingConfig.Redlock));
        var connStr = remoteCacheConnectionString ?? cachingConfig.RemoteCacheConnectionString;
        return services.AddServices(connStr, LocalCacheType,
            distributedLockingEnabled: cachingConfig.DistributedLockingEnabled,
            redisKeyFormat: cachingConfig.Redlock.RedisKeyFormat,
            redisDatabaseId: cachingConfig.RemoteCache.DatabaseId,
            cacheExpiryServiceEnabled: cachingConfig.CacheExpiryServiceEnabled);
    }

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, Action<CachingConfig> configureConfig,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
        if (configureConfig is null) throw new ArgumentNullException(nameof(configureConfig));

        services.AddOptionsWithValidateOnStart<CachingConfig>()
            .Configure(configureConfig)
            .ValidateDataAnnotations();
        //materialise the configuration so registration-time toggles can be honoured
        var cachingConfig = new CachingConfig();
        configureConfig(cachingConfig);
        return services.AddServices(remoteCacheConnectionString ?? cachingConfig.RemoteCacheConnectionString, LocalCacheType,
            cacheExpiryServiceEnabled: cachingConfig.CacheExpiryServiceEnabled);
    }

    private static ConnectionMultiplexer? AddServices(this IServiceCollection services,
        string? remoteCacheConnectionString,
        CacheType LocalCacheType,
        CacheType RemoteCacheType = CacheType.Redis,
        bool distributedLockingEnabled = false,
        string redisKeyFormat = "RedLock:{0}",
        int redisDatabaseId = 0,
        KubernetesProbeTypes healthCheckRedis = KubernetesProbeTypes.None,
        bool cacheExpiryServiceEnabled = true)
    {
        //Registering twice would open a second Redis connection and run a second expiry background service,
        //so the first registration wins and the existing multiplexer is handed back.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedCache)))
            return services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer))
                ?.ImplementationInstance as ConnectionMultiplexer;

        //ensure RedlockConfig is always available (idempotent; won't override bound config from overload #2)
        services.AddOptions<RedlockConfig>();
#if NET8_0_OR_GREATER
        services.TryAddSingleton(TimeProvider.System);
#endif

        if (LocalCacheType == CacheType.Memory)
        {
            //services.AddMemoryCache();//now added via MemoryCacheService
            services.TryAddSingleton<ILocalCache, MemoryCacheService>();
        }
        else if (LocalCacheType == CacheType.Disk)
            services.TryAddSingleton<ILocalCache, DiskCacheService>();
        else
            throw new NotSupportedException($"{nameof(LocalCacheType)} {LocalCacheType} is not supported!");

        services.TryAddSingleton<IDistributedCache, DistributedCacheService>();

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

            services.TryAddSingleton<IRemoteCache, RedisCacheService>();
            services.TryAddSingleton<RemoteCacheExpiryService>();
            services.TryAddSingleton<LocalCacheExpiryService>();
            if (cacheExpiryServiceEnabled)
                services.AddHostedService<CacheExpiryBgService>();

            var multiplexer = GetMultiplexer(remoteCacheConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

#if NET8_0_OR_GREATER
            if (healthCheckRedis != KubernetesProbeTypes.None)
                services.AddHealthChecks()
                    .AddRedis(multiplexer, tags: healthCheckRedis.GetTags());
#endif

            if (distributedLockingEnabled)
            {
                var rlMuxer = (RedLockMultiplexer)multiplexer;
                rlMuxer.RedisKeyFormat = redisKeyFormat;
                rlMuxer.RedisDatabase = redisDatabaseId;
                var redLockFactory = RedLockFactory.Create([rlMuxer]);
                services.AddSingleton<IDistributedLockFactory>(redLockFactory);
            }

            //We return the ConnectionMultiplexer so it can be reused by other services requiring a Redis connection.
            return multiplexer;
        }

        services.TryAddSingleton<IRemoteCache, NullRemoteCache>();
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
