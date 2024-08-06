namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Integration tests with a dependency on a running Redis instance.
/// </summary>
public class CacheTests : TestBase
{
    public CacheTests(ITestOutputHelper output) : base(output) { }

    [Theory, Trait("Category", nameof(IRemoteCacheService))]
    [InlineData(SerialisationType.Json)]
    [InlineData(SerialisationType.MessagePack)]
    public async Task TestRemoteCacheTTLRetrievalWithLUAScript(SerialisationType RemoteCacheSerialisationType)
    {
        var key = $"{nameof(TestRemoteCacheTTLRetrievalWithLUAScript)}:{RemoteCacheSerialisationType}";
        var expiry = TimeSpan.FromSeconds(10);
        var obj = new MyTestClass();

        var cachingOptions = new CachingOptions
        {
            LoadBuiltInLuaScripts = true,
            RemoteCache = new CacheOptions { SerialisationType = RemoteCacheSerialisationType }
        };
        var services = new ServiceCollection().AddXUnitLogging(_testOutputHelper);
        _ = services.AddCasCapCaching(cachingOptions, remoteCacheConnectionString);
        var serviceProvider = services.BuildServiceProvider();
        var remoteCacheSvc = serviceProvider.GetRequiredService<IRemoteCacheService>();

        MyTestClass fromCache;
        if (RemoteCacheSerialisationType == SerialisationType.Json)
        {
            //insert into cache
            var json = obj.ToJSON();
            var result = await remoteCacheSvc.SetAsync(key, json, expiry);

            //retrieve from cache
            var resultString = await remoteCacheSvc.GetAsync(key);
            Assert.NotNull(resultString);
            Assert.Equal(json, resultString);
            fromCache = resultString.FromJSON<MyTestClass>();
        }
        else if (RemoteCacheSerialisationType == SerialisationType.MessagePack)
        {
            //insert into cache
            var bytes = obj.ToMessagePack();
            var result = await remoteCacheSvc.SetAsync(key, bytes, expiry);

            //retrieve from cache
            var resultBytes = await remoteCacheSvc.GetBytesAsync(key);
            Assert.NotNull(resultBytes);
            Assert.Equal(bytes, resultBytes);
            fromCache = resultBytes.FromMessagePack<MyTestClass>();
        }
        else
            throw new NotSupportedException();
        Assert.Equal(obj, fromCache);

        //sleep 1 second
        await Task.Delay(1_000);

        var t1 = remoteCacheSvc.GetCacheEntryWithTTL_Lua<MyTestClass>(key);
        var t2 = remoteCacheSvc.GetCacheEntryWithTTL<MyTestClass>(key);
        var tasks = await Task.WhenAll(t1, t2);

        //retrieve object from cache + ttl info
        {
            var result2a = tasks[0];
            Assert.NotEqual(default, result2a);

            Assert.Equal(obj, result2a.cacheEntry);
            Assert.True(result2a.expiry.Value.TotalSeconds < expiry.TotalSeconds);
        }
        {
            var result2b = tasks[1];
            Assert.NotEqual(default, result2b);

            Assert.Equal(obj, result2b.cacheEntry);
        }
    }

    [Fact, Trait("Category", nameof(IDistributedCacheService))]
    public async Task CacheTest()
    {
        //Arrange
        var key = $"{nameof(CacheTest)}";
        var ttl = 5;
        var obj = new MyTestClass();

        //Act
        //insert into cache
        await _distCacheSvc.Set(key, obj, ttl);

        //retrieve from cache
        var result = await _distCacheSvc.Get<MyTestClass>(key);

        //Assert
        Assert.Equal(obj, result);
    }

    [Fact, Trait("Category", nameof(IDistributedCacheService))]
    public async Task CacheAsidePattern_Manual()
    {
        var key = $"{nameof(CacheAsidePattern_Manual)}";
        var ttl = 60;

        var cacheEntry = await _distCacheSvc.Get<MyTestClass>(key);
        if (cacheEntry is null)
        {
            cacheEntry = new MyTestClass();
            await _distCacheSvc.Set<MyTestClass>(key, cacheEntry, ttl);
        }

        var cacheEntry2 = await _distCacheSvc.Get<MyTestClass>(key);
        Assert.NotNull(cacheEntry2);

        Assert.Equal(cacheEntry, cacheEntry2);
    }

    [Fact, Trait("Category", nameof(IDistributedCacheService))]
    public async Task CacheAsidePattern_Auto()
    {
        var key = $"{nameof(CacheAsidePattern_Auto)}";
        var ttl = 60;

        //var cacheEntry = await _distCacheSvc.Get<MyTestClass>(key, new Func<Task>(() => return APIService.GetAsync());
        //var cacheEntry = await _distCacheSvc.Get<MyTestClass>(key, APIService.GetAsync()));
        //var cacheEntry = await _distCacheSvc.Get<MyTestClass>(key, async () => { return await APIService.GetAsync(); }));

        //var cacheEntry = await _distCacheSvc.Get<MyTestClass>(key, async () => { await Task.Yield(); });
        var cacheEntry = await _distCacheSvc.Get(key, () => APIService.GetAsync(), ttl);
        Assert.NotNull(cacheEntry);

        var cacheEntry2 = await _distCacheSvc.Get(key, () => APIService.GetAsync());
        Assert.NotNull(cacheEntry2);

        //todo: need to override object Equals() for this to work?
        Assert.Equal(cacheEntry, cacheEntry2);
    }

    [Theory, Trait("Category", "ExtensionMethods")]
    [InlineData(CacheType.Memory, 1)]
    [InlineData(CacheType.Memory, 2)]
    [InlineData(CacheType.Memory, 3)]
    [InlineData(CacheType.Memory, 4)]
    [InlineData(CacheType.Disk, 1)]
    [InlineData(CacheType.Disk, 2)]
    [InlineData(CacheType.Disk, 3)]
    [InlineData(CacheType.Disk, 4)]
    public void ExtensionMethodsTest1(CacheType LocalCacheType, int extensionType)
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var key = $"{nameof(CacheTests)}:{nameof(ExtensionMethodsTest1)}";
        var cacheEntry = new MyTestClass();

        //Act
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

            };
            services.AddCasCapCaching(cachingOptions);
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
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();


        var localCacheSvc = serviceProvider.GetRequiredService<ILocalCacheService>();
        //var distCacheSvc = serviceProvider.GetRequiredService<IDistributedCacheService>();
        //var remoteCacheSvc = serviceProvider.GetRequiredService<IRemoteCacheService>();

        localCacheSvc.SetLocal(key, cacheEntry);
        var cacheResult = localCacheSvc.Get<MyTestClass>(key);

        //Assert
        Assert.NotNull(loggerFactory);
        Assert.NotNull(localCacheSvc);
        Assert.NotNull(cacheResult);
        Assert.Equal(cacheResult, cacheEntry);
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
