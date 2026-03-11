namespace CasCap.Common.Net.Tests;

/// <summary>
/// Base class for HTTP client tests, providing xUnit logging and configuration.
/// </summary>
public abstract class TestBase : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    /// <summary>Initializes a new instance of the <see cref="TestBase"/> class.</summary>
    protected TestBase(ITestOutputHelper testOutputHelper)
    {
        var configuration = new ConfigurationBuilder()
            .Build();

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(testOutputHelper);

        //assign services to be tested
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
