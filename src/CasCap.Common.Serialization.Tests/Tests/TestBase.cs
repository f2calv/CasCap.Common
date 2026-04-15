namespace CasCap.Common.Serialization.Tests;

/// <summary>Base class for serialization tests, providing xUnit logging.</summary>
public abstract class TestBase : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    /// <summary>Initializes a new instance of the <see cref="TestBase"/> class.</summary>
    protected TestBase(ITestOutputHelper testOutputHelper)
    {
        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
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
