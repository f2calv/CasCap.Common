#if NET8_0_OR_GREATER
using HealthChecks.Redis;
#endif
using CasCap.Common.Validators;
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
        //Routed through the delegate overload so all four overloads honour the same CachingConfig defaults.
        => services.AddCasCapCaching(static _ => { }, remoteCacheConnectionString, LocalCacheType);

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, IConfiguration configuration,
        string? sectionName = null,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(configuration);
#else
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));
#endif

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedCache)))
            return GetRegisteredMultiplexer(services);

        sectionName ??= CachingConfig.ConfigurationSectionName;
        var section = configuration.GetSection(sectionName);
        services.AddOptionsWithValidateOnStart<CachingConfig>()
            .Bind(section)
            .ValidateDataAnnotations();
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
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(cachingConfig);
#else
        if (cachingConfig is null) throw new ArgumentNullException(nameof(cachingConfig));
#endif

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedCache)))
            return GetRegisteredMultiplexer(services);

        services.AddOptionsWithValidateOnStart<CachingConfig>()
            .Configure(options =>
            {
                options.RemoteCacheConnectionString = cachingConfig.RemoteCacheConnectionString;
                options.PubSubPrefix = cachingConfig.PubSubPrefix;
                options.MemoryCacheSizeLimit = cachingConfig.MemoryCacheSizeLimit;
                options.MemoryCacheItemPriority = cachingConfig.MemoryCacheItemPriority;
                options.UseBuiltInLuaScripts = cachingConfig.UseBuiltInLuaScripts;
                options.MemoryCache = cachingConfig.MemoryCache;
                options.DiskCache = cachingConfig.DiskCache;
                options.RemoteCache = cachingConfig.RemoteCache;
                options.DiskCacheFolder = cachingConfig.DiskCacheFolder;
                options.LocalCacheInvalidationEnabled = cachingConfig.LocalCacheInvalidationEnabled;
                options.CacheExpiryServiceEnabled = cachingConfig.CacheExpiryServiceEnabled;
                options.LocalCacheExpiryServiceEnabled = cachingConfig.LocalCacheExpiryServiceEnabled;
                options.RemoteCacheExpiryServiceEnabled = cachingConfig.RemoteCacheExpiryServiceEnabled;
                options.ExpirationSyncMode = cachingConfig.ExpirationSyncMode;
                options.DistributedLockingEnabled = cachingConfig.DistributedLockingEnabled;
                options.CacheKeyFormat = cachingConfig.CacheKeyFormat;
                options.Redlock = cachingConfig.Redlock;
                options.CacheAsideDisabled = cachingConfig.CacheAsideDisabled;
                options.HealthCheckRedis = cachingConfig.HealthCheckRedis;
            })
            .ValidateDataAnnotations();
        var connStr = remoteCacheConnectionString ?? cachingConfig.RemoteCacheConnectionString;
        return services.AddServices(connStr, LocalCacheType,
            distributedLockingEnabled: cachingConfig.DistributedLockingEnabled,
            redisKeyFormat: cachingConfig.Redlock.RedisKeyFormat,
            redisDatabaseId: cachingConfig.RemoteCache.DatabaseId,
            healthCheckRedis: cachingConfig.HealthCheckRedis,
            cacheExpiryServiceEnabled: cachingConfig.CacheExpiryServiceEnabled);
    }

    /// <inheritdoc cref="AddCasCapCaching(IServiceCollection, string?, CacheType)"/>
    public static ConnectionMultiplexer? AddCasCapCaching(this IServiceCollection services, Action<CachingConfig> configureConfig,
        string? remoteCacheConnectionString = null, CacheType LocalCacheType = CacheType.Memory)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(configureConfig);
#else
        if (configureConfig is null) throw new ArgumentNullException(nameof(configureConfig));
#endif

        var cachingConfig = new CachingConfig();
        configureConfig(cachingConfig);
        return services.AddCasCapCaching(cachingConfig, remoteCacheConnectionString, LocalCacheType);
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
            return GetRegisteredMultiplexer(services);

        //ensure RedlockConfig is always available (idempotent; won't override bound config from overload #2)
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CachingConfig>, CachingConfigValidator>());
        services.TryAddSingleton(provider => Microsoft.Extensions.Options.Options.Create(
            provider.GetRequiredService<IOptions<CachingConfig>>().Value.Redlock));
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

        ConnectionMultiplexer? multiplexer = null;
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

            multiplexer = GetMultiplexer(remoteCacheConnectionString);
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

        }
        else
            services.TryAddSingleton<IRemoteCache, NullRemoteCache>();
#if NET8_0_OR_GREATER
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDistributedLockFactory)))
            services.TryAddSingleton<ILeadershipRunner, LeadershipRunner>();
#endif
        return multiplexer;
    }

    private static ConnectionMultiplexer GetMultiplexer(string remoteCacheConnectionString)
    {
        var configurationOptions = ConfigurationOptions.Parse(remoteCacheConnectionString);
        configurationOptions.ClientName = $"{AppDomain.CurrentDomain.FriendlyName}-{Environment.MachineName}";
        var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);
        return multiplexer;
    }

    private static ConnectionMultiplexer? GetRegisteredMultiplexer(IServiceCollection services)
        => services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer))
            ?.ImplementationInstance as ConnectionMultiplexer;
}
