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

    /// <summary>Redis database id used by integration tests, isolated per target framework.</summary>
    /// <remarks>
    /// The test project multi-targets, so each target framework's test process runs concurrently against
    /// the same Redis server. Tests that use <c>ClearOnStartup</c> flush the whole database, which would wipe
    /// keys written by another framework's process. Isolating each framework to its own database id (net8.0 → 8,
    /// net9.0 → 9, net10.0 → 10) prevents cross-process interference.
    /// </remarks>
#if NET10_0_OR_GREATER
    protected const int remoteCacheDatabaseId = 10;
#elif NET9_0
    protected const int remoteCacheDatabaseId = 9;
#else
    protected const int remoteCacheDatabaseId = 8;
#endif
}
