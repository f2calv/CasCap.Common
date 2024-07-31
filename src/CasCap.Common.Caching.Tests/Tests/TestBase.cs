﻿namespace CasCap.Common.Caching.Tests;

public abstract class TestBase
{
    protected IDistributedCacheService _distCacheSvc;
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
        _ = services.AddCasCapCaching("localhost:6379");

        //assign services to be tested
        var serviceProvider = services.BuildServiceProvider();
        _distCacheSvc = serviceProvider.GetRequiredService<IDistributedCacheService>();
        _redisSvc = serviceProvider.GetRequiredService<IRedisCacheService>();
    }
}
