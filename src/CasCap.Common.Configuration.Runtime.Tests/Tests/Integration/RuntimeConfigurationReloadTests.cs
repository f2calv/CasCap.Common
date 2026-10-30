using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class RuntimeConfigurationReloadTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task FeatureControl_IsEnforcedByServerWithoutRestart()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var host = CreateHost();
        using var client = host.CreateClient();
        var initial = await WaitForAsync(client, value => value.Controls.Verbosity == 3);
        using var disabled = await client.GetAsync("/feature", TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, disabled.StatusCode);
        await PublishAsync(new RuntimeControls { FeatureEnabled = true, Verbosity = 4 }, 1);
        await WaitForAsync(client, value => value.Controls.FeatureEnabled);
        using var enabled = await client.GetAsync("/feature", TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, enabled.StatusCode);
        await PublishAsync(new RuntimeControls { FeatureEnabled = false, Verbosity = 5 }, 2);
        var final = await WaitForAsync(client, value => !value.Controls.FeatureEnabled);
        Assert.Equal(initial.Sentinel, final.Sentinel);
        using var disabledAgain = await client.GetAsync("/feature", TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, disabledAgain.StatusCode);
    }

    [Fact]
    public async Task CapturedAggregate_RemainsConsistentAndDeploymentConfigurationIsUnchanged()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3, Categories = ["original"] }, 0);
        await using var host = CreateHost();
        using var client = host.CreateClient();
        await WaitForAsync(client, value => value.Controls.Verbosity == 3);
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<RuntimeControls>>();
        var captured = monitor.CurrentValue;
        var deployment = host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var originalConnection = deployment["ConnectionStrings:RuntimeConfiguration"];
        await PublishAsync(new RuntimeControls { Verbosity = 4, Categories = ["replacement"] }, 1);
        await WaitForAsync(client, value => value.Controls.Verbosity == 4);
        Assert.Equal(3, captured.Verbosity);
        Assert.Equal(["original"], captured.Categories);
        Assert.Equal(["replacement"], monitor.CurrentValue.Categories);
        Assert.Equal(originalConnection, deployment["ConnectionStrings:RuntimeConfiguration"]);
        Assert.Null(deployment["Verbosity"]);
    }

    [Fact]
    public async Task ListenerFailure_IsReportedWithoutTransactionalRollback()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        var failures = new RuntimeConfigurationFailureLog();
        await using var host = new RuntimeConfigurationHostFactory(_connectionString, _environment,
            configureServices: services => services.AddLogging(logging => logging.AddProvider(failures)));
        using var client = host.CreateClient();
        await WaitForAsync(client, value => value.Controls.Verbosity == 3);
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<RuntimeControls>>();
        var failNext = 1;
        using var listener = monitor.OnChange((_, _) =>
        {
            if (Interlocked.Exchange(ref failNext, 0) == 1)
                throw new InvalidOperationException("Private listener detail must not be exported.");
        });
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 1);
        await failures.Failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(5, monitor.CurrentValue.Verbosity);
        Assert.DoesNotContain(failures.Messages, message => message.Contains("Private listener detail", StringComparison.Ordinal));
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 2);
        await WaitForAsync(client, value => value.Controls.Verbosity == 4);
    }

    [Fact]
    public async Task ConsumerApplyFailure_IsDistinctFromAcceptedOptions()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var host = CreateHost();
        using var client = host.CreateClient();
        var initial = await WaitForAsync(client, value => value.Controls.Verbosity == 3);
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<RuntimeControls>>();
        var applied = 3;
        var rejected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = monitor.OnChange((value, _) =>
        {
            if (value.Verbosity == 5)
                rejected.TrySetResult(true);
            else
                Volatile.Write(ref applied, value.Verbosity);
        });
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 1);
        Assert.True(await rejected.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(5, monitor.CurrentValue.Verbosity);
        Assert.Equal(3, Volatile.Read(ref applied));
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 2);
        await WaitForAsync(client, value => value.Controls.Verbosity == 4 && Volatile.Read(ref applied) == 4);
        Assert.Equal(initial.Sentinel, (await WaitForAsync(client, value => value.Controls.Verbosity == 4)).Sentinel);
    }

}
