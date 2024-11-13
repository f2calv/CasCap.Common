using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Threading;

namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Integration tests with a dependency on a running Redis instance.
/// </summary>
public class CacheTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
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
        var objInitial = new MyTestClass(DateTime.UtcNow);
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
        MyTestClass? objFromCache;
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
            objFromCache = jsonFromCache.FromJson<MyTestClass>();
        }
        else if (RemoteCacheSerializationType == SerializationType.MessagePack)
        {
            //serialize
            bytes = objInitial.ToMessagePack();
            //insert into cache
            inserted = remoteCache.Set(key, bytes, expiry);
            //retrieve from cache
            bytesFromCache = remoteCache.GetBytes(key);
            if (bytesFromCache is null)
                throw new NullReferenceException($"{nameof(bytesFromCache)} should not be null here");
            //deserialize
            objFromCache = bytesFromCache.FromMessagePack<MyTestClass>();
        }
        else
            throw new NotSupportedException($"{nameof(RemoteCacheSerializationType)} {RemoteCacheSerializationType} is not supported!");

        //Assert
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
        var objInitial = new MyTestClass(DateTime.UtcNow);
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
        MyTestClass? objFromCache;
        if (SerializationType == SerializationType.Json)
        {
            //serialize
            json = objInitial.ToJson();
            //insert into cache
            inserted = await remoteCache.SetAsync(key, json, expiry);
            //retrieve from cache
            jsonFromCache = await remoteCache.GetAsync(key);
            if (string.IsNullOrWhiteSpace(jsonFromCache))
                throw new NullReferenceException($"{nameof(jsonFromCache)} should not be null here");
            //deserialize
            objFromCache = jsonFromCache.FromJson<MyTestClass>();
        }
        else if (SerializationType == SerializationType.MessagePack)
        {
            //serialize
            bytes = objInitial.ToMessagePack();
            //insert into cache
            inserted = await remoteCache.SetAsync(key, bytes, expiry);
            //retrieve from cache
            bytesFromCache = await remoteCache.GetBytesAsync(key);
            if (bytesFromCache is null)
                throw new NullReferenceException($"{nameof(bytesFromCache)} should not be null here");
            //deserialize
            objFromCache = bytesFromCache.FromMessagePack<MyTestClass>();
        }
        else
            throw new NotSupportedException($"{nameof(SerializationType)} {SerializationType} is not supported!");

        Assert.True(inserted);

        //Assert
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
        var key = $"{Guid.NewGuid()}:{nameof(RemoteCacheSvc_Sync)}:{SerializationType}";
        var expiry = TimeSpan.FromSeconds(10);
        var objInitial = new MyTestClass(DateTime.UtcNow);
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
        if (SerializationType == SerializationType.Json)
            inserted = await remoteCache.SetAsync(key, objInitial.ToJson(), expiry);
        else if (SerializationType == SerializationType.MessagePack)
            inserted = await remoteCache.SetAsync(key, objInitial.ToMessagePack(), expiry);
        var objFromCacheTask1 = remoteCache.GetCacheEntryWithTTL_Lua<MyTestClass>(key);
        var objFromCacheTask2 = remoteCache.GetCacheEntryWithTTL<MyTestClass>(key);
        //retrieve object from cache + ttl info via StackExchange and custom Lua
        var tasks = await Task.WhenAll(objFromCacheTask1, objFromCacheTask2);
        var objFromCache1 = tasks[0];
        var objFromCache2 = tasks[1];
        //cleanup
        var deleted = remoteCache.Delete(key);
        //check cleaned up
        var notfound = remoteCache.Get(key);

        //Assert
        Assert.True(inserted);

        Assert.NotEqual(default, objFromCache1);
        Assert.Equal(objInitial, objFromCache1.cacheEntry);
        Assert.NotNull(objFromCache1.expiry);
        Assert.True(objFromCache1.expiry.Value.TotalSeconds <= expiry.TotalSeconds);

        Assert.NotEqual(default, objFromCache2);
        Assert.Equal(objInitial, objFromCache2.cacheEntry);
        Assert.NotNull(objFromCache2.expiry);
        Assert.True(objFromCache2.expiry.Value.TotalSeconds <= expiry.TotalSeconds);

        Assert.True(deleted);
        Assert.Null(notfound);
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
        var ttl = Debugger.IsAttached ? 60 : 5;
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
        var cacheEntry = await distCacheSvc.Get<MyTestClass>(key);
        if (cacheEntry is null)
            cacheEntry = new MyTestClass(DateTime.UtcNow);
        else
            throw new Exception("object should not exist in the cache at the start of the test");
        //insert into both local and remote cache
        await distCacheSvc.Set(key, cacheEntry, ttl);
        //retrieve from dist cache (i.e. get from local)
        var result1 = await distCacheSvc.Get<MyTestClass>(key);
        //delete from localCache to force retrieve from remote
        var result2 = localCache.Get<MyTestClass>(key);
        var isDeleted1 = localCache.Delete(key);
        //retrieve from dist cache (i.e. now will need to get from remote)
        var result3 = await distCacheSvc.Get<MyTestClass>(key);
        //delete from both caches
        var isDeleted2 = await distCacheSvc.Delete(key);
        //re-retrieve from cache
        var result4 = await distCacheSvc.Get<MyTestClass>(key);
        //test async Func setter
        //var cacheEntry = await distCacheSvc.Get<MyTestClass>(key, new Func<Task>(() => return APIService.GetAsync());
        //var cacheEntry = await distCacheSvc.Get<MyTestClass>(key, APIService.GetAsync()));
        //var cacheEntry = await distCacheSvc.Get<MyTestClass>(key, async () => { return await APIService.GetAsync(); }));
        //var cacheEntry = await distCacheSvc.Get<MyTestClass>(key, async () => { await Task.Yield(); });
        var cacheEntryA = await distCacheSvc.Get(key, () => APIService.GetAsync(), ttl);
        var cacheEntryB = await distCacheSvc.Get(key, () => APIService.GetAsync());

        var isDeleted3 = await distCacheSvc.Delete(key);
        //stop bg service
        await source.CancelAsync();
        await Task.Delay(1_000);//short pause for the cancellation token to take effect
        //await localCacheInvalidationBgSvc.StopAsync(cancellationToken);

        //Assert
        Assert.Equal(cacheEntry, result1);
        Assert.Equal(cacheEntry, result2);
        Assert.True(isDeleted1);
        Assert.Equal(cacheEntry, result3);
        Assert.Null(result4);
        Assert.True(isDeleted2);
        Assert.NotNull(cacheEntryA);
        Assert.NotNull(cacheEntryB);
        Assert.Equal(cacheEntryA, cacheEntryB);
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
        var cacheEntry = new MyTestClass(DateTime.UtcNow);

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
        localCache.Set(key, cacheEntry);
        var cacheResult = localCache.Get<MyTestClass>(key);
        var deleteSuccess = localCache.Delete(key);
        var deleteFailure = localCache.Delete(Guid.NewGuid().ToString());
        localCache.Set("test123", new MyTestClass(DateTime.UtcNow));
        var countDeleted = localCache.DeleteAll();

        //Assert
        Assert.NotNull(loggerFactory);
        Assert.NotNull(localCache);
        Assert.NotNull(cacheResult);
        Assert.Equal(cacheResult, cacheEntry);
        Assert.True(deleteSuccess);
        Assert.False(deleteFailure);
        Assert.Equal(1, countDeleted);
    }
}

//mock API for testing fake async data retrieval
public class APIService
{
    static MyTestClass? obj = null;

    public static async Task<MyTestClass> GetAsync()
    {
        await Task.Delay(0);
        await Task.Delay(0);
        //lets go fake getting some data
        obj ??= new MyTestClass(DateTime.UtcNow);
        return obj;
    }
}

[MessagePackObject(true)]
public class MyTestClass
{
    public MyTestClass(DateTime utcNow)
    {
        ID = 1337;
    }

    public int ID { get; init; }

    public DateTime utcNow { get; init; }

    public override bool Equals(object? obj)
    {
        return obj is MyTestClass @class &&
               ID == @class.ID &&
               utcNow == @class.utcNow;
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
