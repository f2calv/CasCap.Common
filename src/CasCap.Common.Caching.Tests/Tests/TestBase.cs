namespace CasCap.Common.Caching.Tests;

/// <summary>Base class for caching integration tests, providing a Redis connection string constant.</summary>
public abstract class TestBase(ITestOutputHelper testOutputHelper)
{
    /// <summary>Provides xUnit test output.</summary>
    protected ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;
    //protected IDistributedCacheService _distCacheSvc;
    //protected ILocalCacheService _localCache;

    /// <summary>Redis connection string used by integration tests.</summary>
    protected const string remoteCacheConnectionString = "localhost:6379,allowAdmin=true";
}
