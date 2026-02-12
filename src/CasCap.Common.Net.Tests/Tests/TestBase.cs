namespace CasCap.Common.Net.Tests;

public abstract class TestBase
{
    protected readonly IServiceProvider _serviceProvider;

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
}
