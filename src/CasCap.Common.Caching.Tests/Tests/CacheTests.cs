using Microsoft.Extensions.Hosting;
using System.Threading;

namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Integration tests with a dependency on a running Redis instance.
/// </summary>
public class CacheTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory, Trait("Category", nameof(IRemoteCacheService))]
    [InlineData(SerialisationType.Json, true, CacheType.Memory)]
    [InlineData(SerialisationType.Json, false, CacheType.Memory)]
    [InlineData(SerialisationType.Json, true, CacheType.Disk)]
    [InlineData(SerialisationType.Json, false, CacheType.Disk)]
    [InlineData(SerialisationType.MessagePack, true, CacheType.Memory)]
    [InlineData(SerialisationType.MessagePack, false, CacheType.Memory)]
    [InlineData(SerialisationType.MessagePack, true, CacheType.Disk)]
    [InlineData(SerialisationType.MessagePack, false, CacheType.Disk)]
    public void RemoteCacheSvc_Sync(SerialisationType RemoteCacheSerialisationType, bool ClearOnStartup, CacheType LocalCacheType)
    {
        //Arrange
        var key = $"{nameof(RemoteCacheSvc_Sync)}:{RemoteCacheSerialisationType}:{LocalCacheType}";
        var expiry = TimeSpan.FromSeconds(10);
        var obj = new MyTestClass();
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions
            {
                SerialisationType = RemoteCacheSerialisationType,
                ClearOnStartup = ClearOnStartup
            },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString, LocalCacheType);
        var remoteCacheSvc = services.BuildServiceProvider().GetRequiredService<IRemoteCacheService>();

        //Act
        var result = false;
        string json = null, resultJson = null;
        byte[] bytes = null, resultBytes = null;
        MyTestClass fromCache;
        if (RemoteCacheSerialisationType == SerialisationType.Json)
        {
            //serialise
            json = obj.ToJSON();
            //insert into cache
            result = remoteCacheSvc.Set(key, json, expiry);
            //retrieve from cache
            resultJson = remoteCacheSvc.Get(key);
            //deserialise
            fromCache = resultJson.FromJSON<MyTestClass>();
        }
        else if (RemoteCacheSerialisationType == SerialisationType.MessagePack)
        {
            //serialise
            bytes = obj.ToMessagePack();
            //insert into cache
            result = remoteCacheSvc.Set(key, bytes, expiry);
            //retrieve from cache
            resultBytes = remoteCacheSvc.GetBytes(key);
            //deserialise
            fromCache = resultBytes.FromMessagePack<MyTestClass>();
        }
        else
            throw new NotSupportedException($"{nameof(RemoteCacheSerialisationType)} {RemoteCacheSerialisationType} is not supported!");

        //Assert
        if (RemoteCacheSerialisationType == SerialisationType.Json)
        {
            Assert.NotNull(resultJson);
            Assert.Equal(json, resultJson);
        }
        else if (RemoteCacheSerialisationType == SerialisationType.MessagePack)
        {
            Assert.NotNull(resultBytes);
            Assert.Equal(bytes, resultBytes);
        }
        Assert.Equal(obj, fromCache);
    }

    [Theory, Trait("Category", nameof(IRemoteCacheService))]
    [InlineData(SerialisationType.Json, true)]
    [InlineData(SerialisationType.Json, false)]
    [InlineData(SerialisationType.MessagePack, true)]
    [InlineData(SerialisationType.MessagePack, false)]
    public async Task RemoteCacheSvc_Async(SerialisationType SerialisationType, bool ClearOnStartup)
    {
        //Arrange
        var key = $"{nameof(RemoteCacheSvc_Async)}:{SerialisationType}";
        var expiry = TimeSpan.FromSeconds(10);
        var obj = new MyTestClass();
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions { ClearOnStartup = ClearOnStartup, SerialisationType = SerialisationType },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString);
        var remoteCacheSvc = services.BuildServiceProvider().GetRequiredService<IRemoteCacheService>();

        //Act
        var result = false;
        string json = null, resultJson = null;
        byte[] bytes = null, resultBytes = null;
        MyTestClass fromCache;
        if (SerialisationType == SerialisationType.Json)
        {
            //serialise
            json = obj.ToJSON();
            //insert into cache
            result = await remoteCacheSvc.SetAsync(key, json, expiry);
            //retrieve from cache
            resultJson = await remoteCacheSvc.GetAsync(key);
            //deserialise
            fromCache = resultJson.FromJSON<MyTestClass>();
        }
        else if (SerialisationType == SerialisationType.MessagePack)
        {
            //serialise
            bytes = obj.ToMessagePack();
            //insert into cache
            result = await remoteCacheSvc.SetAsync(key, bytes, expiry);
            //retrieve from cache
            resultBytes = await remoteCacheSvc.GetBytesAsync(key);
            //deserialise
            fromCache = resultBytes.FromMessagePack<MyTestClass>();
        }
        else
            throw new NotSupportedException($"{nameof(SerialisationType)} {SerialisationType} is not supported!");

        //Assert
        if (SerialisationType == SerialisationType.Json)
        {
            Assert.NotNull(resultJson);
            Assert.Equal(json, resultJson);
        }
        else if (SerialisationType == SerialisationType.MessagePack)
        {
            Assert.NotNull(resultBytes);
            Assert.Equal(bytes, resultBytes);
        }
        Assert.Equal(obj, fromCache);
    }

    [Theory, Trait("Category", nameof(IRemoteCacheService))]
    [InlineData(SerialisationType.Json)]
    [InlineData(SerialisationType.MessagePack)]
    public async Task RemoteCacheSvc_LuaTest(SerialisationType SerialisationType, bool ClearOnStartup = true)
    {
        //Arrange
        var key = $"{nameof(RemoteCacheSvc_Sync)}:{SerialisationType}";
        var expiry = TimeSpan.FromSeconds(10);
        var obj = new MyTestClass();
        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions { ClearOnStartup = ClearOnStartup, SerialisationType = SerialisationType },
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString);
        var remoteCacheSvc = services.BuildServiceProvider().GetRequiredService<IRemoteCacheService>();

        //Act
        var inserted = false;
        if (SerialisationType == SerialisationType.Json)
            inserted = await remoteCacheSvc.SetAsync(key, obj.ToJSON(), expiry);
        else if (SerialisationType == SerialisationType.MessagePack)
            inserted = await remoteCacheSvc.SetAsync(key, obj.ToMessagePack(), expiry);
        var t1 = remoteCacheSvc.GetCacheEntryWithTTL_Lua<MyTestClass>(key);
        var t2 = remoteCacheSvc.GetCacheEntryWithTTL<MyTestClass>(key);
        //retrieve object from cache + ttl info via StackExchange and custom Lua
        var tasks = await Task.WhenAll(t1, t2);
        var result1 = tasks[0];
        var result2 = tasks[1];
        //cleanup
        var deleted = remoteCacheSvc.Delete(key);
        //check cleaned up
        var notfound = remoteCacheSvc.Get(key);

        //Assert
        Assert.True(inserted);

        Assert.NotEqual(default, result1);
        Assert.Equal(obj, result1.cacheEntry);
        Assert.True(result1.expiry.Value.TotalSeconds <= expiry.TotalSeconds);

        Assert.NotEqual(default, result2);
        Assert.Equal(obj, result2.cacheEntry);
        Assert.True(result2.expiry.Value.TotalSeconds <= expiry.TotalSeconds);

        Assert.True(deleted);
        Assert.Null(notfound);
    }

    [Theory, Trait("Category", nameof(IDistributedCacheService))]
    [InlineData(SerialisationType.Json, CacheType.Memory)]
    [InlineData(SerialisationType.Json, CacheType.Disk)]
    [InlineData(SerialisationType.MessagePack, CacheType.Memory)]
    [InlineData(SerialisationType.MessagePack, CacheType.Disk)]
    public async Task DistCacheSvc_Test(SerialisationType SerialisationType, CacheType LocalCacheType)
    {
        //Arrange
        var key = $"{nameof(DistCacheSvc_Test)}:{SerialisationType}";
        var ttl = 5;
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        var cachingOptions = new CachingOptions
        {
            RemoteCache = new CacheOptions { ClearOnStartup = true, SerialisationType = SerialisationType },
            DiskCache = new CacheOptions { ClearOnStartup = true, SerialisationType = SerialisationType },
            MemoryCache = new CacheOptions { ClearOnStartup = true }
        };
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString, LocalCacheType);
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.AddStaticLogging();
        var distCacheSvc = serviceProvider.GetRequiredService<IDistributedCacheService>();
        var localCacheSvc = serviceProvider.GetRequiredService<ILocalCacheService>();
        var localCacheInvalidationBgSvc = serviceProvider.GetRequiredService<IHostedService>() as LocalCacheInvalidationBgService;
        var source = new CancellationTokenSource();
        var cancellationToken = source.Token;

        //Act
        //start bg service
        await localCacheInvalidationBgSvc.StartAsync(cancellationToken);
        //check if object exists
        var cacheEntry = await distCacheSvc.Get<MyTestClass>(key);
        if (cacheEntry is null)
            cacheEntry = new MyTestClass();
        else
            throw new Exception("object should not exist in the cache at the start of the test");
        //insert into both local and remote cache
        await distCacheSvc.Set(key, cacheEntry, ttl);
        //retrieve from dist cache (i.e. get from local)
        var result1 = await distCacheSvc.Get<MyTestClass>(key);
        //delete from localCache to force retrieve from remote
        var result2 = localCacheSvc.Get<MyTestClass>(key);
        var isDeleted1 = localCacheSvc.Delete(key);
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
        var key = $"{nameof(CacheTests)}:{nameof(ServiceCollectionSetupTests)}";
        var cacheEntry = new MyTestClass();

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
        var localCacheSvc = serviceProvider.GetRequiredService<ILocalCacheService>();
        localCacheSvc.Set(key, cacheEntry);
        var cacheResult = localCacheSvc.Get<MyTestClass>(key);
        var deleteSuccess = localCacheSvc.Delete(key);
        var deleteFailure = localCacheSvc.Delete(Guid.NewGuid().ToString());
        localCacheSvc.Set("test123", new MyTestClass());
        var countDeleted = localCacheSvc.DeleteAll();

        //Assert
        Assert.NotNull(loggerFactory);
        Assert.NotNull(localCacheSvc);
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
    static MyTestClass obj = null;

    public static async Task<MyTestClass> GetAsync()
    {
        await Task.Delay(0);
        await Task.Delay(0);
        //lets go fake getting some data
        obj ??= new MyTestClass();
        return obj;
    }
}

[MessagePackObject(true)]
public class MyTestClass
{
    public int ID { get; set; } = 1337;
    public DateTime utcNow { get; set; } = DateTime.UtcNow;

    public override bool Equals(object obj)
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
