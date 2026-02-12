namespace CasCap.Common.Extensions.Tests;

/// <summary>
/// Base class for extension method tests, providing xUnit logging.
/// </summary>
public abstract class TestBase : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    protected TestBase(ITestOutputHelper testOutputHelper)
    {
        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddXUnitLogging(testOutputHelper);

        //assign services to be tested
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
