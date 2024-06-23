namespace CasCap.Common.Serialisation.Tests;

public abstract class TestBase
{
    //protected I???Service _???Svc;

    public TestBase(ITestOutputHelper output)
    {
        var configuration = new ConfigurationBuilder()
            //.AddCasCapConfiguration()
            .Build();

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        //add services
        //services.Add???();

        //assign services to be tested
        _ = services.BuildServiceProvider();
        //_???Svc = serviceProvider.GetRequiredService<I???Service>();
    }
}
