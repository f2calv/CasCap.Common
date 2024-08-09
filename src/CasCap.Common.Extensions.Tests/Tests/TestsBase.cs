namespace CasCap.Common.Extensions.Tests;

public abstract class TestBase
{
    public TestBase(ITestOutputHelper output)
    {
        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddXUnitLogging(output);

        //assign services to be tested
        _ = services.BuildServiceProvider();
    }
}
