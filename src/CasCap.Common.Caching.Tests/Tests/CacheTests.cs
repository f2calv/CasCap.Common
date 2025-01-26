using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Integration tests with a dependency on a running Redis instance.
/// </summary>
public class CacheTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact]
    public async Task SlidingExpirationTest_Async()
    {
        //Arrange
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions { ClearOnStartup = true, SerializationType = SerializationType.Json },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString);
        var remoteCache = services.BuildServiceProvider().GetRequiredService<IRemoteCache>();

        var key = $"{Guid.NewGuid()}:{nameof(SlidingExpirationTest_Async)}:{SerializationType.Json}";
        var slidingExpirationSeconds = 6;
        var checkIntervalSeconds = 2;
        var slidingExpiration = TimeSpan.FromSeconds(slidingExpirationSeconds);
        //var expiration = DateTime.UtcNow.AddSeconds(10);
        var objInitial = new MockDto(DateTime.UtcNow);

        //Act
        var added = await remoteCache.SetAsync(key, objInitial.ToJson(), slidingExpiration: slidingExpiration);
        for (var i = 0; i < slidingExpirationSeconds; i += checkIntervalSeconds)
        {
            //each time this is called the slidingExpiration should be reset...
            //var result = await remoteCache.GetCacheEntryWithTTL<MockDto>(key);//wont work because it doesn't use GETEX
            var result = await remoteCache.GetCacheEntryWithTTL_Lua<MockDto>(key);

            Assert.NotNull(result.cacheEntry);
            Assert.NotNull(result.expiry);
            Assert.True(result.expiry.Value.TotalSeconds >= slidingExpirationSeconds - checkIntervalSeconds);
            await Task.Delay(checkIntervalSeconds * 1000, CancellationToken.None);
        }
        await Task.Delay(slidingExpirationSeconds * 1000, CancellationToken.None);
        var exists = await remoteCache.GetAsync(key);
        Assert.Null(exists);
    }

    [Fact]
    public async Task AbsoluteExpirationTest_Async()
    {
        //Arrange
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions { ClearOnStartup = true, SerializationType = SerializationType.Json },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString);
        var remoteCache = services.BuildServiceProvider().GetRequiredService<IRemoteCache>();

        var key = $"{Guid.NewGuid()}:{nameof(AbsoluteExpirationTest_Async)}:{SerializationType.Json}";
        var absoluteExpirationSeconds = 5;
        var absoluteExpiration = DateTime.UtcNow.AddSeconds(absoluteExpirationSeconds);
        var objInitial = new MockDto(DateTime.UtcNow);

        //Act
        var added = await remoteCache.SetAsync(key, objInitial.ToJson(), absoluteExpiration: absoluteExpiration);
        var exists = await remoteCache.GetAsync(key);
        //check 1 second before it expires
        await Task.Delay((absoluteExpirationSeconds - 1) * 1000, CancellationToken.None);
        var stillExists = await remoteCache.GetAsync(key);
        //check 1 second after it expires
        await Task.Delay(2 * 1000, CancellationToken.None);
        var hasExpired = await remoteCache.GetAsync(key);

        //Assert
        Assert.True(added);
        Assert.NotNull(exists);
        Assert.NotNull(stillExists);
        Assert.Null(hasExpired);
    }

    [Theory, Trait("Category", nameof(IRemoteCache))]
    [InlineData(SerializationType.Json, true, CacheType.Memory)]
    [InlineData(SerializationType.Json, false, CacheType.Memory)]
    [InlineData(SerializationType.Json, true, CacheType.Disk)]
    [InlineData(SerializationType.Json, false, CacheType.Disk)]
    [InlineData(SerializationType.MessagePack, true, CacheType.Memory)]
    [InlineData(SerializationType.MessagePack, false, CacheType.Memory)]
    [InlineData(SerializationType.MessagePack, true, CacheType.Disk)]
    [InlineData(SerializationType.MessagePack, false, CacheType.Disk)]
    public void RemoteCacheSvc_Sync(SerializationType RemoteCacheSerializationType, bool ClearOnStartup, CacheType LocalCacheType)
    {
        //Arrange
        var key = $"{Guid.NewGuid()}:{nameof(RemoteCacheSvc_Sync)}:{RemoteCacheSerializationType}:{LocalCacheType}";
        var expiry = TimeSpan.FromSeconds(10);
        var expiration = DateTime.UtcNow.AddSeconds(10);
        var objInitial = new MockDto(DateTime.UtcNow);
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions
            {
                SerializationType = RemoteCacheSerializationType,
                ClearOnStartup = ClearOnStartup
            },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString, LocalCacheType);
        var remoteCache = services.BuildServiceProvider().GetRequiredService<IRemoteCache>();

        //Act
        var inserted = false;
        string? json = null, jsonFromCache = null;
        byte[]? bytes = null, bytesFromCache = null;
        MockDto? objFromCache;
        if (RemoteCacheSerializationType == SerializationType.Json)
        {
            //serialize
            json = objInitial.ToJson();
            //insert into cache
            inserted = remoteCache.Set(key, json, expiry);
            //retrieve from cache
            jsonFromCache = remoteCache.Get(key);
            if (string.IsNullOrWhiteSpace(jsonFromCache))
                throw new NullReferenceException($"{nameof(jsonFromCache)} should not be null here");
            //deserialize
            objFromCache = jsonFromCache.FromJson<MockDto>();
        }
        else if (RemoteCacheSerializationType == SerializationType.MessagePack)
        {
            //serialize
            bytes = objInitial.ToMessagePack();
            //insert into cache
            inserted = remoteCache.Set(key, bytes, expiry);
            if (!inserted)
                throw new NullReferenceException($"{nameof(inserted)} should be true here");
            //retrieve from cache
            bytesFromCache = remoteCache.GetBytes(key);
            if (bytesFromCache is null)
                throw new NullReferenceException($"{nameof(bytesFromCache)} should not be null here");
            //deserialize
            objFromCache = bytesFromCache.FromMessagePack<MockDto>();
        }
        else
            throw new NotSupportedException($"{nameof(RemoteCacheSerializationType)} {RemoteCacheSerializationType} is not supported!");

        //Assert
        Assert.True(DateTime.UtcNow < expiration);
        Assert.True(inserted);
        if (RemoteCacheSerializationType == SerializationType.Json)
        {
            Assert.NotNull(jsonFromCache);
            Assert.Equal(json, jsonFromCache);
        }
        else if (RemoteCacheSerializationType == SerializationType.MessagePack)
        {
            Assert.NotNull(bytesFromCache);
            Assert.Equal(bytes, bytesFromCache);
        }
        Assert.Equal(objInitial, objFromCache);
    }

    [Theory, Trait("Category", nameof(IRemoteCache))]
    [InlineData(SerializationType.Json, true)]
    [InlineData(SerializationType.Json, false)]
    [InlineData(SerializationType.MessagePack, true)]
    [InlineData(SerializationType.MessagePack, false)]
    public async Task RemoteCacheSvc_Async(SerializationType SerializationType, bool ClearOnStartup)
    {
        //Arrange
        var key = $"{Guid.NewGuid()}:{nameof(RemoteCacheSvc_Async)}:{SerializationType}";
        var expiry = TimeSpan.FromSeconds(10);
        var expiration = DateTime.UtcNow.AddSeconds(10);
        var objInitial = new MockDto(DateTime.UtcNow);
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions { ClearOnStartup = ClearOnStartup, SerializationType = SerializationType },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString);
        var remoteCache = services.BuildServiceProvider().GetRequiredService<IRemoteCache>();

        //Act
        var inserted = false;
        string? json = null, jsonFromCache = null;
        byte[]? bytes = null, bytesFromCache = null;
        MockDto? objFromCache;
        if (SerializationType == SerializationType.Json)
        {
            //serialize
            json = objInitial.ToJson();
            //insert into cache
            inserted = await remoteCache.SetAsync(key, json, expiry);
            if (!inserted)
                throw new NullReferenceException($"{nameof(inserted)} should be true here");
            //retrieve from cache
            jsonFromCache = await remoteCache.GetAsync(key);
            if (string.IsNullOrWhiteSpace(jsonFromCache))
                throw new NullReferenceException($"{nameof(jsonFromCache)} should not be null here");
            //deserialize
            objFromCache = jsonFromCache.FromJson<MockDto>();
        }
        else if (SerializationType == SerializationType.MessagePack)
        {
            //serialize
            bytes = objInitial.ToMessagePack();
            //insert into cache
            inserted = await remoteCache.SetAsync(key, bytes, expiry);
            if (!inserted)
                throw new NullReferenceException($"{nameof(inserted)} should be true here");
            //retrieve from cache
            bytesFromCache = await remoteCache.GetBytesAsync(key);
            if (bytesFromCache is null)
                throw new NullReferenceException($"{nameof(bytesFromCache)} should not be null here");
            //deserialize
            objFromCache = bytesFromCache.FromMessagePack<MockDto>();
        }
        else
            throw new NotSupportedException($"{nameof(SerializationType)} {SerializationType} is not supported!");

        //Assert
        Assert.True(DateTime.UtcNow < expiration);
        Assert.True(inserted);
        if (SerializationType == SerializationType.Json)
        {
            Assert.NotNull(jsonFromCache);
            Assert.Equal(json, jsonFromCache);
        }
        else if (SerializationType == SerializationType.MessagePack)
        {
            Assert.NotNull(bytesFromCache);
            Assert.Equal(bytes, bytesFromCache);
        }
        Assert.Equal(objInitial, objFromCache);
    }

    [Theory, Trait("Category", nameof(IRemoteCache))]
    [InlineData(SerializationType.Json)]
    [InlineData(SerializationType.MessagePack)]
    public async Task RemoteCacheSvc_LuaTest(SerializationType SerializationType, bool ClearOnStartup = true)
    {
        //Arrange
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions { ClearOnStartup = ClearOnStartup, SerializationType = SerializationType },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString);
        var remoteCache = services.BuildServiceProvider().GetRequiredService<IRemoteCache>();
        var key = $"{Guid.NewGuid()}:{nameof(RemoteCacheSvc_LuaTest)}:{SerializationType}";
        var totalSeconds = 10;
        var absoluteExpiration = DateTime.UtcNow.AddSeconds(totalSeconds);
        var objInitial = new MockDto(DateTime.UtcNow);

        //Act
        var inserted = false;
        if (SerializationType == SerializationType.Json)
            inserted = await remoteCache.SetAsync(key, objInitial.ToJson(), absoluteExpiration: absoluteExpiration);
        else if (SerializationType == SerializationType.MessagePack)
            inserted = await remoteCache.SetAsync(key, objInitial.ToMessagePack(), absoluteExpiration: absoluteExpiration);
        var objFromCacheTask1 = remoteCache.GetCacheEntryWithTTL_Lua<MockDto>(key);
        var objFromCacheTask2 = remoteCache.GetCacheEntryWithTTL<MockDto>(key);
        //retrieve object from cache + ttl info via StackExchange and custom Lua
        var tasks = await Task.WhenAll(objFromCacheTask1, objFromCacheTask2);
        var objFromCache1 = tasks[0];
        var objFromCache2 = tasks[1];
        //cleanup
        var shouldBeTrue = remoteCache.Delete(key);
        //check cleaned up
        var shouldBeNull = remoteCache.Get(key);

        //Assert
        Assert.True(DateTime.UtcNow < absoluteExpiration);//check the above tests havent taken too long!
        Assert.True(inserted);

        Assert.NotEqual(default, objFromCache1);
        Assert.Equal(objInitial, objFromCache1.cacheEntry);
        Assert.NotNull(objFromCache1.expiry);
        Assert.True(objFromCache1.expiry.Value.TotalSeconds <= totalSeconds);

        Assert.NotEqual(default, objFromCache2);
        Assert.Equal(objInitial, objFromCache2.cacheEntry);
        Assert.NotNull(objFromCache2.expiry);
        Assert.True(objFromCache2.expiry.Value.TotalSeconds <= totalSeconds);

        Assert.True(shouldBeTrue);
        Assert.Null(shouldBeNull);
    }

    [Theory, Trait("Category", nameof(IDistributedCache))]
    [InlineData(SerializationType.Json, CacheType.Memory)]
    [InlineData(SerializationType.Json, CacheType.Disk)]
    [InlineData(SerializationType.MessagePack, CacheType.Memory)]
    [InlineData(SerializationType.MessagePack, CacheType.Disk)]
    public async Task DistCacheSvc_Test(SerializationType SerializationType, CacheType LocalCacheType)
    {
        //Arrange
        var key = $"{Guid.NewGuid()}:{nameof(DistCacheSvc_Test)}:{SerializationType}";
        var absoluteExpiration = Debugger.IsAttached ? DateTimeOffset.UtcNow.AddSeconds(60) : DateTimeOffset.UtcNow.AddSeconds(5);
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        var cachingOptions = new CachingOptions
        {
            RemoteCache = new CacheOptions { ClearOnStartup = true, SerializationType = SerializationType },
            DiskCache = new CacheOptions { ClearOnStartup = true, SerializationType = SerializationType },
            MemoryCache = new CacheOptions { ClearOnStartup = true }
        };
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString, LocalCacheType);
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.AddStaticLogging();
        var distCacheSvc = serviceProvider.GetRequiredService<IDistributedCache>();
        var localCache = serviceProvider.GetRequiredService<ILocalCache>();
        var localCacheInvalidationBgSvc = serviceProvider.GetRequiredService<IHostedService>() as LocalCacheInvalidationBgService;
        var source = new CancellationTokenSource();
        var cancellationToken = source.Token;

        //Act
        //start bg service
        await localCacheInvalidationBgSvc!.StartAsync(cancellationToken);
        //check if object exists
        var objInitial = await distCacheSvc.Get<MockDto>(key);
        if (objInitial is null)
            objInitial = new MockDto(DateTime.UtcNow);
        else
            throw new Exception("object should not exist in the cache at the start of the test");
        //insert into both local and remote cache
        await distCacheSvc.Set(key, objInitial, absoluteExpiration: absoluteExpiration);
        //retrieve from dist cache (i.e. get from local)
        var objFromCache1 = await distCacheSvc.Get<MockDto>(key);
        //delete from localCache to force retrieve from remote
        var objFromCache2 = localCache.Get<MockDto>(key);
        var isDeleted1 = localCache.Delete(key);
        //retrieve from dist cache (i.e. now will need to get from remote)
        var objFromCache3 = await distCacheSvc.Get<MockDto>(key);
        //delete from both caches
        var isDeleted2 = await distCacheSvc.Delete(key);
        //re-retrieve from cache
        var objFromCache4 = await distCacheSvc.Get<MockDto>(key);
        //test async Func setter
        //var objFromCache = await distCacheSvc.Get<MyTestClass>(key, new Func<Task>(() => return APIService.GetAsync());
        //var objFromCache = await distCacheSvc.Get<MyTestClass>(key, APIService.GetAsync()));
        //var objFromCache = await distCacheSvc.Get<MyTestClass>(key, async () => { return await APIService.GetAsync(); }));
        //var objFromCache = await distCacheSvc.Get<MyTestClass>(key, async () => { await Task.Yield(); });
        var objFromCacheA = await distCacheSvc.Get(key, MockApiService.GetAsync, absoluteExpiration: absoluteExpiration);
        var objFromCacheB = await distCacheSvc.Get(key, MockApiService.GetAsync);

        var isDeleted3 = await distCacheSvc.Delete(key);
        //stop bg service
        await source.CancelAsync();
        await Task.Delay(1_000);//short pause for the cancellation token to take effect
        //await localCacheInvalidationBgSvc.StopAsync(cancellationToken);

        //Assert
        Assert.Equal(objInitial, objFromCache1);
        Assert.Equal(objInitial, objFromCache2);
        Assert.True(isDeleted1);
        Assert.Equal(objInitial, objFromCache3);
        Assert.Null(objFromCache4);
        Assert.True(isDeleted2);
        Assert.NotNull(objFromCacheA);
        Assert.NotNull(objFromCacheB);
        Assert.Equal(objFromCacheA, objFromCacheB);
        Assert.True(isDeleted3);
    }

    [Theory, Trait("Category", "ServiceCollection")]
    [InlineData(CacheType.Memory, 1)]
    [InlineData(CacheType.Memory, 2)]
    [InlineData(CacheType.Memory, 3)]
    [InlineData(CacheType.Memory, 4)]
    [InlineData(CacheType.Disk, 1)]
    [InlineData(CacheType.Disk, 2)]
    [InlineData(CacheType.Disk, 3)]
    [InlineData(CacheType.Disk, 4)]
    public void ServiceCollectionSetupTests(CacheType LocalCacheType, int extensionType)
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var key = $"{Guid.NewGuid()}:{nameof(CacheTests)}:{nameof(ServiceCollectionSetupTests)}";
        var objInitial = new MockDto(DateTime.UtcNow);

        if (extensionType == 1)
            services.AddCasCapCaching(LocalCacheType: LocalCacheType);
        else if (extensionType == 2)
        {
            var configuration = new ConfigurationBuilder()
                //.AddCasCapConfiguration()
                .AddJsonFile($"appsettings.Test.json", optional: false, reloadOnChange: false)
                .Build();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddCasCapCaching(configuration, LocalCacheType: LocalCacheType);
        }
        else if (extensionType == 3)
        {
            var cachingOptions = new CachingOptions
            {
                //DiskCacheFolder = "testing123"
            };
            services.AddCasCapCaching(cachingOptions, LocalCacheType: LocalCacheType);
        }
        else if (extensionType == 4)
        {
            services.AddCasCapCaching(options =>
            {
                // Specify default option values
                //options.DiskCacheFolder = "testing123";
            }, LocalCacheType: LocalCacheType);
        }

        var serviceProvider = services.BuildServiceProvider();

        //Act
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var localCache = serviceProvider.GetRequiredService<ILocalCache>();
        localCache.Set(key, objInitial);
        var objFromCache = localCache.Get<MockDto>(key);
        var deleteSuccess = localCache.Delete(key);
        var deleteFailure = localCache.Delete(Guid.NewGuid().ToString());
        localCache.Set("test123", new MockDto(DateTime.UtcNow));
        var countDeleted = localCache.DeleteAll();

        //Assert
        Assert.NotNull(loggerFactory);
        Assert.NotNull(localCache);
        Assert.NotNull(objFromCache);
        Assert.Equal(objFromCache, objInitial);
        Assert.True(deleteSuccess);
        Assert.False(deleteFailure);
        Assert.Equal(1, countDeleted);
    }
}
