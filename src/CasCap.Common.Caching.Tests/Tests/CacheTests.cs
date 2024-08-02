using MessagePack;
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
    public async Task TestRedisTTLRetrievalWithLUAScript(SerialisationType RemoteCacheSerialisationType)
    {
        //todo: test alongside newly discovered Redis method which returns TTL
        var key = $"{RemoteCacheSerialisationType}:{nameof(TestRedisTTLRetrievalWithLUAScript)}";
        var expiry = TimeSpan.FromSeconds(10);
        var obj = new MyTestClass();

        MyTestClass fromCache;
        if (RemoteCacheSerialisationType == SerialisationType.Json)
        {
            //insert into cache
            var json = obj.ToJSON();
            var result = await _remoteCacheSvc.SetAsync(key, json, expiry);

            //simple retrieve from cache
            var result1 = await _remoteCacheSvc.GetAsync(key);
            Assert.NotNull(result1);
            fromCache = result1.FromJSON<MyTestClass>();
        }
        else if (RemoteCacheSerialisationType == SerialisationType.MessagePack)
        {
            //insert into cache
            var bytes = obj.ToMessagePack();
            var result = await _remoteCacheSvc.SetAsync(key, bytes, expiry);

            //simple retrieve from cache
            var result1 = await _remoteCacheSvc.GetBytesAsync(key);
            Assert.NotNull(result1);
            fromCache = result1.FromMessagePack<MyTestClass>();
        }
        else
            throw new NotSupportedException();
        Assert.Equal(obj.ToJSON(), fromCache.ToJSON());//when bytes re-serialised they will never be the same object, so check the contents via json

        //TODO: exit tests early, need to refactor these
        return;
        /*
        //sleep 1 second
        await Task.Delay(1_000);

        var t1 = _remoteCacheSvc.GetCacheEntryWithTTL_Lua<MyTestClass>(key);
        var t2 = _remoteCacheSvc.GetCacheEntryWithTTL<MyTestClass>(key);
        var tasks = await Task.WhenAll(t1, t2);

        //retrieve object from cache + ttl info
        {
            var result2a = tasks[0];
            Assert.NotEqual(default, result2a);

            Assert.Equal(obj.ToJSON(), result2a.cacheEntry.ToJSON());
            Assert.True(result2a.expiry.Value.TotalSeconds < expiry.TotalSeconds);
        }
        {
            var result2b = tasks[1];
            Assert.NotEqual(default, result2b);

            Assert.Equal(obj.ToJSON(), result2b.cacheEntry.ToJSON());
        */
    }

    [Fact, Trait("Category", nameof(IDistributedCacheService))]
    public async Task CacheTest()
    {
        var key = $"{nameof(CacheTest)}";
        var ttl = 60;

        //insert into cache
        var obj = new MyTestClass();
        await _distCacheSvc.Set(key, obj, ttl);

        //simple retrieve from cache
        var result = await _distCacheSvc.Get<MyTestClass>(key);
        Assert.Equal(obj.ToJSON(), result.ToJSON());
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

        Assert.Equal(cacheEntry.ToJSON(), cacheEntry2.ToJSON());
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
        Assert.Equal(cacheEntry.ToJSON(), cacheEntry2.ToJSON());
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
}
