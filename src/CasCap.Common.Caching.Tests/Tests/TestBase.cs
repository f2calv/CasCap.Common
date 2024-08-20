namespace CasCap.Common.Caching.Tests;

public abstract class TestBase
{
    protected ITestOutputHelper _testOutputHelper;
    //protected IDistributedCacheService _distCacheSvc;
    //protected ILocalCacheService _localCacheSvc;

    protected const string remoteCacheConnectionString = "localhost:6379,allowAdmin=true";

    public TestBase(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        /*
        //var cachingOptions = Options.Create(new CachingOptions
        //{
        //    MemoryCacheSizeLimit = 100,
        //    //LoadBuiltInLuaScripts = true,
        //    //RemoteCache = new CacheOptions() { SerializationType = SerializationType.Json },
        //});

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddXUnitLogging(testOutputHelper)
            //.AddSingleton(cachingOptions)
            ;

        //add services
        _ = services.AddCasCapCaching(remoteCacheConnectionString);

        //assign services to be tested
        var serviceProvider = services.BuildServiceProvider();
        _distCacheSvc = serviceProvider.GetRequiredService<IDistributedCacheService>();
        _localCacheSvc = serviceProvider.GetRequiredService<ILocalCacheService>();
        */
    }
}
