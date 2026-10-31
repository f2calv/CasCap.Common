using Microsoft.Extensions.Configuration;

namespace CasCap.Common.Services.Tests;

public sealed class HostLifecycleConfigTests
{
    [Fact, Trait("Category", "Hosting")]
    public void Registration_BindsShutdownTimeoutAndExitState()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{HostLifecycleConfig.ConfigurationSectionName}:{nameof(HostLifecycleConfig.ShutdownTimeoutMs)}"]
                    = "45000",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplicationHostLifecycle(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(
            TimeSpan.FromSeconds(45),
            provider.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
        Assert.Equal(0, provider.GetApplicationExitCode());
    }

    [Fact, Trait("Category", "Hosting")]
    public void Registration_RejectsInvalidShutdownTimeout()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{HostLifecycleConfig.ConfigurationSectionName}:{nameof(HostLifecycleConfig.ShutdownTimeoutMs)}"]
                    = "999",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplicationHostLifecycle(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<HostLifecycleConfig>>().Value);
    }
}
