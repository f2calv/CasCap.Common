namespace CasCap.Common.Caching.Tests;

public abstract class TestBase(ITestOutputHelper testOutputHelper)
{
    protected ITestOutputHelper _testOutputHelper = testOutputHelper;
    //protected IDistributedCacheService _distCacheSvc;
    //protected ILocalCacheService _localCache;

    protected const string remoteCacheConnectionString = "localhost:6379,allowAdmin=true";
}
