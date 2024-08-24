namespace CasCap.Common.Net.Tests;

public abstract class TestBase
{
    public TestBase(ITestOutputHelper testOutputHelper)
    {
        var configuration = new ConfigurationBuilder()
            .Build();

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(testOutputHelper);

        //assign services to be tested
        _ = services.BuildServiceProvider();
        //_???Svc = serviceProvider.GetRequiredService<I???Service>();
    }
}
