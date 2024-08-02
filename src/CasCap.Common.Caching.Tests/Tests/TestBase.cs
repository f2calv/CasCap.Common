namespace CasCap.Common.Caching.Tests;

public abstract class TestBase
{
    protected IDistributedCacheService _distCacheSvc;
    protected IRemoteCacheService _remoteCacheSvc;

    public TestBase(ITestOutputHelper output)
    {
        var cachingOptions = Options.Create(new CachingOptions
        {
            MemoryCacheSizeLimit = 100,
            LoadBuiltInLuaScripts = true,
            RemoteCacheSerialisationType = SerialisationType.Json,
        });

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddXUnitLogging(output)
            .AddSingleton(cachingOptions);

        //add services
        _ = services.AddCasCapCaching("localhost:6379");

        //assign services to be tested
        var serviceProvider = services.BuildServiceProvider();
        _distCacheSvc = serviceProvider.GetRequiredService<IDistributedCacheService>();
        _remoteCacheSvc = serviceProvider.GetRequiredService<IRemoteCacheService>();
    }
}
