namespace CasCap.Common.Caching.Tests;

/// <summary>
/// Base class for caching integration tests, providing a Redis connection string constant.
/// </summary>
public abstract class TestBase(ITestOutputHelper testOutputHelper)
{
    protected ITestOutputHelper _testOutputHelper = testOutputHelper;
    //protected IDistributedCacheService _distCacheSvc;
    //protected ILocalCacheService _localCache;

    protected const string remoteCacheConnectionString = "localhost:6379,allowAdmin=true";
}
