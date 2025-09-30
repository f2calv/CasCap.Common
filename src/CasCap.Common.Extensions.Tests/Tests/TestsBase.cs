namespace CasCap.Common.Extensions.Tests;

public abstract class TestBase
{
    protected TestBase(ITestOutputHelper testOutputHelper)
    {
        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddXUnitLogging(testOutputHelper);

        //assign services to be tested
        _ = services.BuildServiceProvider();
    }
}
