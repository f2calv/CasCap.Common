namespace CasCap.Common.Caching.Tests;

/// <summary>Provides shared Test-environment configuration and output for caching tests.</summary>
public abstract class TestBase : IDisposable
{
    private readonly ConfigurationManager _configuration = new();

    protected TestBase(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
        _configuration.AddStandardConfiguration("Test", typeof(TestBase).Assembly);
    }

    /// <summary>Provides xUnit test output.</summary>
    protected ITestOutputHelper TestOutputHelper { get; }

    /// <summary>Provides configuration with environment variables taking final precedence.</summary>
    protected IConfiguration Configuration => _configuration;

    /// <summary>Redis connection string used by integration tests.</summary>
    protected string remoteCacheConnectionString => Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("The Redis test connection is required.");

    /// <inheritdoc/>
    public void Dispose()
    {
        _configuration.Dispose();
        GC.SuppressFinalize(this);
    }

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
