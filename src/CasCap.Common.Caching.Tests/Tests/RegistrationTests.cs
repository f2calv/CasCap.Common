using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Reflection;

namespace CasCap.Common.Caching.Tests;

/// <summary>Tests caching registration without requiring a Redis connection.</summary>
[Trait("Category", "Registration")]
public class RegistrationTests
{
    [Fact]
    public void Registration_CopiesEveryOption()
    {
        var supplied = new CachingConfig
        {
            RemoteCacheConnectionString = "cache.example.test:6379",
            PubSubPrefix = "test-prefix",
            MemoryCacheSizeLimit = 1234,
            MemoryCacheItemPriority = CacheItemPriority.High,
            UseBuiltInLuaScripts = true,
            MemoryCache = new CacheParameters { SerializationType = SerializationType.Json },
            DiskCache = new CacheParameters { SerializationType = SerializationType.MessagePack },
            RemoteCache = new CacheParameters { SerializationType = SerializationType.None, DatabaseId = 7 },
            DiskCacheFolder = Path.Combine(Path.GetTempPath(), "cascap-test-cache"),
            LocalCacheInvalidationEnabled = false,
            CacheExpiryServiceEnabled = false,
            LocalCacheExpiryServiceEnabled = false,
            RemoteCacheExpiryServiceEnabled = false,
            ExpirationSyncMode = ExpirationSyncType.ExtendRemoteExpiry,
            DistributedLockingEnabled = true,
            CacheKeyFormat = "test:{0}",
            Redlock = new RedlockConfig { RedisKeyFormat = "TestLock:{0}" },
            CacheAsideDisabled = true,
            HealthCheckRedis = KubernetesProbeTypes.Readiness
        };
        var services = new ServiceCollection();
        //No connection string is passed, so no Redis connection is opened by this registration.
        services.AddCasCapCaching(supplied with { RemoteCacheConnectionString = null });
        using var serviceProvider = services.BuildServiceProvider();

        var resolved = serviceProvider.GetRequiredService<IOptions<CachingConfig>>().Value;

        var defaults = new CachingConfig();
        foreach (var property in typeof(CachingConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod is null)
                continue;
            if (property.Name == nameof(CachingConfig.RemoteCacheConnectionString))
                continue;

            var expected = property.GetValue(supplied);
            Assert.NotEqual(property.GetValue(defaults), expected);
            var actual = property.GetValue(resolved);
            if (expected is IEnumerable expectedItems and not string)
                Assert.Equal(expectedItems.Cast<object>(), ((IEnumerable)actual!).Cast<object>());
            else
                Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Registration_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddCasCapCaching();
        services.AddCasCapCaching();

        //Asserted on the descriptors so the caches do not have to be activated.
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILocalCache));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRemoteCache));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDistributedCache));
    }
}
