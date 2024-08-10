namespace CasCap.Common.Serialisation.Tests;

public abstract class TestBase
{
    public TestBase(ITestOutputHelper testOutputHelper)
    {
        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddXUnitLogging(testOutputHelper);

        //assign services to be tested
        var serviceProvider = services.BuildServiceProvider();

        ApplicationLogging.LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }
}
