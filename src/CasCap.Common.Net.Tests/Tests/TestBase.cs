using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
namespace CasCap.Common.Net.Tests;

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
        var serviceProvider = services.BuildServiceProvider();
        //_???Svc = serviceProvider.GetRequiredService<I???Service>();
    }
}