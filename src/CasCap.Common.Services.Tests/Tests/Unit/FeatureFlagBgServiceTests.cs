namespace CasCap.Common.Services.Tests;

/// <summary>Tests the lifecycle and fault-observation contract of <see cref="FeatureFlagBgService"/>.</summary>
public sealed class FeatureFlagBgServiceTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Fact, Trait("Category", "BackgroundService")]
    public async Task FiniteSiblingCompletion_ContinuesObservingActiveChild()
    {
        var finiteCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finiteFeature = new TestBgFeature("Finite", _ =>
        {
            finiteCompleted.TrySetResult(true);
            return Task.CompletedTask;
        });
        var activeFeature = new TestBgFeature("Active", async cancellationToken =>
        {
            activeStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        using var sut = CreateService([finiteFeature, activeFeature], "Finite", "Active");

        await sut.StartAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(finiteCompleted.Task, activeStarted.Task)
            .WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.False(sut.ExecuteTask!.IsCompleted);

        await sut.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(sut.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact, Trait("Category", "BackgroundService")]
    public async Task LaterFault_PropagatesAfterFiniteSiblingCompletes()
    {
        var expected = new InvalidOperationException("later failure");
        var faultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finiteFeature = new TestBgFeature("Finite", _ => Task.CompletedTask);
        var faultingFeature = new TestBgFeature("Faulting", _ =>
        {
            activeStarted.TrySetResult(true);
            return faultSource.Task;
        });
        using var sut = CreateService([finiteFeature, faultingFeature], "Finite", "Faulting");

        await sut.StartAsync(TestContext.Current.CancellationToken);
        await activeStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        faultSource.SetException(expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteTask!);
        Assert.Same(expected, actual);
    }

    [Fact, Trait("Category", "BackgroundService")]
    public async Task FirstFault_PropagatesWhileSiblingRuns()
    {
        var expected = new InvalidOperationException("first failure");
        var siblingStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var faultingFeature = new TestBgFeature("Faulting", _ => Task.FromException(expected));
        var activeFeature = new TestBgFeature("Active", async cancellationToken =>
        {
            siblingStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        using var sut = CreateService([faultingFeature, activeFeature], "Faulting", "Active");

        await sut.StartAsync(TestContext.Current.CancellationToken);
        await siblingStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteTask!);
        Assert.Same(expected, actual);
    }

    [Fact, Trait("Category", "BackgroundService")]
    public async Task AllChildrenComplete_ThrowsInvalidOperationException()
    {
        var firstFeature = new TestBgFeature("First", _ => Task.CompletedTask);
        var secondFeature = new TestBgFeature("Second", _ => Task.CompletedTask);
        using var sut = CreateService([firstFeature, secondFeature], "First", "Second");

        await sut.StartAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteTask!);
        Assert.Contains("First, Second", exception.Message);
    }

    [Fact, Trait("Category", "BackgroundService")]
    public async Task AllChildrenComplete_AfterStopApplication_CompletesCleanly()
    {
        var lifetime = new TestHostApplicationLifetime();
        var runOnceFeature = new TestBgFeature("RunOnce", _ =>
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        });
        using var sut = CreateService([runOnceFeature], lifetime, "RunOnce");

        await sut.StartAsync(TestContext.Current.CancellationToken);
        await sut.ExecuteTask!.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.True(sut.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact, Trait("Category", "BackgroundService")]
    public async Task Cancellation_CompletesCleanly()
    {
        var activeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeFeature = new TestBgFeature("Active", async cancellationToken =>
        {
            activeStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        using var sut = CreateService([activeFeature], "Active");

        await sut.StartAsync(TestContext.Current.CancellationToken);
        await activeStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(sut.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact, Trait("Category", "BackgroundService")]
    public async Task DisabledFeature_IsNotExecuted()
    {
        var enabledStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enabledFeature = new TestBgFeature("Enabled", async cancellationToken =>
        {
            enabledStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        var disabledFeature = new TestBgFeature("Disabled", _ => Task.CompletedTask);
        using var sut = CreateService([enabledFeature, disabledFeature], "Enabled");

        await sut.StartAsync(TestContext.Current.CancellationToken);
        await enabledStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, disabledFeature.ExecutionCount);

        await sut.StopAsync(TestContext.Current.CancellationToken);
    }

    private static FeatureFlagBgService CreateService(IEnumerable<IBgFeature> features, params string[] enabledFeatures)
        => CreateService(features, new TestHostApplicationLifetime(), enabledFeatures);

    [Theory, Trait("Category", "BackgroundService")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_AwaitsChildCleanup(bool childFault)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("sibling failure");
        var active = new TestBgFeature("Active", async token =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                cleaning.SetResult();
                await finishCleanup.Task;
            }
        });
        var failing = new TestBgFeature("Faulting", _ => fault.Task);
        using var lifetime = new TestHostApplicationLifetime();
        using var sut = CreateService(childFault ? [active, failing] : [active], lifetime, "Active", "Faulting");
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Task? stopping = null;
        try
        {
            if (childFault)
                fault.SetException(expected);
            else
                stopping = sut.StopAsync(TestContext.Current.CancellationToken);
            await cleaning.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Assert.False(sut.ExecuteTask!.IsCompleted);
            if (childFault)
                Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
        }
        finally
        {
            finishCleanup.TrySetResult();
        }
        if (childFault)
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.ExecuteTask!.WaitAsync(_timeout, TestContext.Current.CancellationToken)));
        else
        {
            await stopping!.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Assert.True(sut.ExecuteTask!.IsCompletedSuccessfully);
        }
    }

    private static FeatureFlagBgService CreateService(
        IEnumerable<IBgFeature> features,
        IHostApplicationLifetime lifetime,
        params string[] enabledFeatures)
        => new(
            NullLogger<FeatureFlagBgService>.Instance,
            Options.Create(new FeatureFlagConfig
            {
                EnabledFeatures = new HashSet<string>(enabledFeatures, StringComparer.OrdinalIgnoreCase),
            }),
            features,
            lifetime,
            new ApplicationExitState());

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();

        public void Dispose() => _stopping.Dispose();
    }

    private sealed class TestBgFeature(string featureName, Func<CancellationToken, Task> executeAsync) : IBgFeature
    {
        private int _executionCount;

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public string FeatureName { get; } = featureName;

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            return executeAsync(cancellationToken);
        }
    }
}
