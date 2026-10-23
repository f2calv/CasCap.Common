using Microsoft.Extensions.Options;

namespace CasCap.Common.Caching.Tests;

/// <summary>Verifies election configuration and registration without opening a Redis connection.</summary>
[Trait("Category", "Integration")]
[Trait("Category", "Leadership")]
public sealed class LeadershipRegistrationTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [InlineData("instance")]
    [InlineData("delegate")]
    [InlineData("configuration")]
    public void Registration_PreservesOneRedlockConfiguration(string registration)
    {
        var config = new RedlockConfig
        {
            ExpiryMs = 101,
            WaitMs = 102,
            RetryMs = 103,
            RedisKeyFormat = "TestLock:{0}",
            LeadershipCheckIntervalMs = 104,
            Profiles = new() { [RedlockProfiles.LeaderElection] = new() { ExpiryMs = 201, WaitMs = 202, RetryMs = 203 } }
        };
        var services = new ServiceCollection();
        switch (registration)
        {
            case "instance":
                services.AddCasCapCaching(new CachingConfig { Redlock = config });
                break;
            case "delegate":
                services.AddCasCapCaching(options => options.Redlock = config);
                break;
            case "configuration":
                var section = "Custom:Caching";
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{section}:Redlock:ExpiryMs"] = "101",
                    [$"{section}:Redlock:WaitMs"] = "102",
                    [$"{section}:Redlock:RetryMs"] = "103",
                    [$"{section}:Redlock:RedisKeyFormat"] = "TestLock:{0}",
                    [$"{section}:Redlock:LeadershipCheckIntervalMs"] = "104",
                    [$"{section}:Redlock:Profiles:LeaderElection:ExpiryMs"] = "201",
                    [$"{section}:Redlock:Profiles:LeaderElection:WaitMs"] = "202",
                    [$"{section}:Redlock:Profiles:LeaderElection:RetryMs"] = "203"
                }).Build();
                services.AddCasCapCaching(configuration, section);
                break;
            default:
                throw new InvalidOperationException("Unknown test registration.");
        }
        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<RedlockConfig>>().Value;
        Assert.Same(provider.GetRequiredService<IOptions<CachingConfig>>().Value.Redlock, resolved);
        Assert.Equal((TimeSpan.FromMilliseconds(101), TimeSpan.FromMilliseconds(102), TimeSpan.FromMilliseconds(103)),
            resolved.GetTimings());
        Assert.Equal((TimeSpan.FromMilliseconds(201), TimeSpan.FromMilliseconds(202), TimeSpan.FromMilliseconds(203)),
            resolved.GetTimings(RedlockProfiles.LeaderElection));
        Assert.Equal(resolved.GetTimings(), resolved.GetTimings(RedlockProfiles.CacheMiss));
        Assert.Equal(resolved.GetTimings(), resolved.GetTimings("missing"));
        Assert.Equal(config.RedisKeyFormat, resolved.RedisKeyFormat);
        Assert.Equal(config.LeadershipCheckIntervalMs, resolved.LeadershipCheckIntervalMs);
        Assert.Null(provider.GetService<IDistributedLockFactory>());
    }

    [Theory]
    [InlineData("root-expiry")]
    [InlineData("root-wait")]
    [InlineData("root-retry")]
    [InlineData("profile-expiry")]
    [InlineData("profile-wait")]
    [InlineData("profile-retry")]
    [InlineData("observation")]
    [InlineData("profiles-null")]
    [InlineData("profile-null")]
    public void InvalidTimings_FailOptionsValidation(string setting)
    {
        var services = new ServiceCollection();
        services.AddCasCapCaching(new CachingConfig
        {
            Redlock = setting switch
            {
                "root-expiry" => new() { ExpiryMs = 0 },
                "root-wait" => new() { WaitMs = 0 },
                "root-retry" => new() { RetryMs = 0 },
                "profile-expiry" => new() { Profiles = new() { [RedlockProfiles.LeaderElection] = new() { ExpiryMs = 0 } } },
                "profile-wait" => new() { Profiles = new() { [RedlockProfiles.LeaderElection] = new() { WaitMs = 0 } } },
                "profile-retry" => new() { Profiles = new() { [RedlockProfiles.LeaderElection] = new() { RetryMs = 0 } } },
                "profiles-null" => new() { Profiles = null! },
                "profile-null" => new() { Profiles = new() { ["example"] = null! } },
                _ => new() { LeadershipCheckIntervalMs = 0 },
            },
        });
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RedlockConfig>>().Value);
    }

    [Fact]
    public void RepeatedRegistration_PreservesTheFirstOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedLockFactory>(new TestLockFactory((_, _) => Task.FromResult<IRedLock>(new TestLease())));
        var first = new RedlockConfig { RedisKeyFormat = "First:{0}", LeadershipCheckIntervalMs = 123 };
        services.AddCasCapCaching(options => options.Redlock = first);
        services.AddCasCapCaching(new CachingConfig { Redlock = new() { RedisKeyFormat = "Second:{0}" } });
        using var provider = services.BuildServiceProvider();
        Assert.Same(first, provider.GetRequiredService<IOptions<RedlockConfig>>().Value);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILeadershipRunner));
    }

    //Regression: an unused runner made local-only hosts fail ValidateOnBuild.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalOnlyCaching_DoesNotSilentlyEnableLeadership(bool distributedLockingEnabled)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCasCapCaching(options =>
        {
            options.RemoteCache = new CacheParameters { IsEnabled = false };
            options.DistributedLockingEnabled = distributedLockingEnabled;
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var config = provider.GetRequiredService<IOptions<RedlockConfig>>().Value;
        Assert.Equal(new RedlockConfig().GetTimings(), config.GetTimings());
        Assert.NotNull(provider.GetRequiredService<IDistributedCache>());
        Assert.Null(provider.GetService<ILeadershipRunner>());
        var error = Assert.Throws<InvalidOperationException>(provider.GetRequiredService<ILeadershipRunner>);
        Assert.Contains(nameof(ILeadershipRunner), error.Message);
    }

    [Fact]
    public void RegisteredFactory_EnablesValidatedLeadership()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDistributedLockFactory>(new TestLockFactory((_, _) => Task.FromResult<IRedLock>(new TestLease())));
        services.AddCasCapCaching(options => options.RemoteCache = new CacheParameters { IsEnabled = false });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.IsType<LeadershipRunner>(provider.GetRequiredService<ILeadershipRunner>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILeadershipRunner));
    }
}
