namespace CasCap.Common.Caching.Tests;

public abstract class TestBase
{
    protected IDistCacheService _distCacheSvc;
    protected IRedisCacheService _redisSvc;

    public TestBase(ITestOutputHelper output)
    {
        var configuration = new ConfigurationBuilder()
            //.AddCasCapConfiguration()
            .AddJsonFile($"appsettings.Test.json", optional: false, reloadOnChange: false)
            .Build();

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        //add services
        services.AddCasCapCaching();

        //assign services to be tested
        var serviceProvider = services.BuildServiceProvider();
        _distCacheSvc = serviceProvider.GetRequiredService<IDistCacheService>();
        _redisSvc = serviceProvider.GetRequiredService<IRedisCacheService>();
    }
}