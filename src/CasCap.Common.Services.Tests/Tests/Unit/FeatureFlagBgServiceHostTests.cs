namespace CasCap.Common.Services.Tests;

/// <summary>Tests <see cref="FeatureFlagBgService"/> through the generic host.</summary>
public sealed class FeatureFlagBgServiceHostTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Fact, Trait("Category", "BackgroundService")]
    public async Task LaterFault_StopsHostWhenStopHostConfigured()
    {
        var faultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var faultingFeatureStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.Configure<HostOptions>(options =>
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);
        builder.Services.AddSingleton<IBgFeature>(new TestBgFeature("Finite", _ => Task.CompletedTask));
        builder.Services.AddSingleton<IBgFeature>(new TestBgFeature("Faulting", _ =>
        {
            faultingFeatureStarted.TrySetResult(true);
            return faultSource.Task;
        }));
        builder.Services.AddFeatureFlagService(new HashSet<string>(["Finite", "Faulting"], StringComparer.OrdinalIgnoreCase));
        using var host = builder.Build();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var stopping = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStopping.Register(() => stopping.TrySetResult(true));

        await host.StartAsync(TestContext.Current.CancellationToken);
        await faultingFeatureStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);

        faultSource.SetException(new InvalidOperationException("later failure"));
        await stopping.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class TestBgFeature(string featureName, Func<CancellationToken, Task> executeAsync) : IBgFeature
    {
        public string FeatureName { get; } = featureName;

        public Task ExecuteAsync(CancellationToken cancellationToken) => executeAsync(cancellationToken);
    }
}