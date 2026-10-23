using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CasCap.Common.Caching.Tests;

/// <summary>Verifies lease ownership, cancellation, cleanup and fault propagation without Redis.</summary>
[Trait("Category", "Leadership")]
public sealed class LeadershipRunnerTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task LeaseLoss_AwaitsCleanupBeforeReleaseAndReacquisition()
    {
        using var stopping = new CancellationTokenSource();
        var first = new TestLease();
        var second = new TestLease();
        var firstStarted = Signal();
        var cleanupStarted = Signal();
        var finishCleanup = Signal();
        var secondStarted = Signal();
        var tokens = new List<CancellationToken>();
        var factory = new TestLockFactory((attempt, _) => Task.FromResult<IRedLock>(attempt == 1 ? first : second));
        var sut = CreateRunner(factory);
        var running = sut.RunAsync("worker", async token =>
        {
            tokens.Add(token);
            if (tokens.Count == 1)
                firstStarted.SetResult();
            else
                secondStarted.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                if (tokens.Count == 1)
                {
                    cleanupStarted.SetResult();
                    await finishCleanup.Task;
                }
            }
        }, stopping.Token);
        try
        {
            await firstStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            first.Lose();
            await cleanupStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Assert.False(first.Disposed);
            Assert.Equal(1, factory.Attempts);
            finishCleanup.SetResult();
            await secondStarted.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Assert.True(first.Disposed);
            Assert.Equal(2, tokens.Count);
            Assert.NotEqual(tokens[0], tokens[1]);
            Assert.True(tokens[0].IsCancellationRequested);
            Assert.False(tokens[1].IsCancellationRequested);
        }
        finally
        {
            finishCleanup.TrySetResult();
            await stopping.CancelAsync();
            await running.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        }
        Assert.True(second.Disposed);
    }

    [Fact]
    public async Task Shutdown_AwaitsCleanupWhileLeaseIsHeld()
    {
        using var stopping = new CancellationTokenSource();
        var lease = new TestLease();
        var started = Signal();
        var cleaning = Signal();
        var finishCleanup = Signal();
        var factory = new TestLockFactory((_, _) => Task.FromResult<IRedLock>(lease));
        var running = CreateRunner(factory).RunAsync("worker", async token =>
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
        }, stopping.Token);
        try
        {
            await started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            await stopping.CancelAsync();
            await cleaning.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Assert.True(lease.IsAcquired);
            Assert.False(lease.Disposed);
            Assert.False(running.IsCompleted);
        }
        finally
        {
            finishCleanup.TrySetResult();
            await stopping.CancelAsync();
            await running.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        }
        Assert.True(lease.Disposed);
        Assert.Equal(1, factory.Attempts);
    }

    [Fact]
    public async Task Contention_DoesNotStartWorkAndPreservesElectionTimings()
    {
        using var stopping = new CancellationTokenSource();
        var standby = new TestLease();
        standby.Lose();
        var acquired = new TestLease();
        var started = Signal();
        var factory = new TestLockFactory((attempt, _) => Task.FromResult<IRedLock>(attempt == 1 ? standby : acquired));
        var running = CreateRunner(factory).RunAsync("worker", async token =>
        {
            Assert.True(standby.Disposed);
            Assert.Equal(2, factory.Attempts);
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, stopping.Token);
        try
        {
            await started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Assert.Equal("worker", factory.Resource);
            Assert.Equal(TimeSpan.FromSeconds(30), factory.Expiry);
            Assert.Equal(TimeSpan.FromSeconds(60), factory.Wait);
            Assert.Equal(TimeSpan.FromMilliseconds(1), factory.Retry);
        }
        finally
        {
            await stopping.CancelAsync();
            await running.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        }
        Assert.True(acquired.Disposed);
    }

    [Fact]
    public async Task AcquisitionCancellation_DoesNotStartWork()
    {
        using var stopping = new CancellationTokenSource();
        var acquiring = Signal();
        var factory = new TestLockFactory(async (_, token) =>
        {
            acquiring.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Acquisition must be cancelled.");
        });
        var executions = 0;
        var running = CreateRunner(factory).RunAsync("worker", _ =>
        {
            executions++;
            return Task.CompletedTask;
        }, stopping.Token);
        await acquiring.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        await stopping.CancelAsync();
        await running.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.Equal(0, executions);
        Assert.Equal(1, factory.Attempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkerFault_ReleasesAndPropagatesWithoutRetry(bool synchronous)
    {
        var expected = new InvalidOperationException("worker failed");
        var lease = new TestLease();
        var factory = new TestLockFactory((_, _) => Task.FromResult<IRedLock>(lease));
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRunner(factory).RunAsync("worker", _ =>
                synchronous ? throw expected : Task.FromException(expected), TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
        Assert.True(lease.Disposed);
        Assert.Equal(1, factory.Attempts);
    }

    [Fact]
    public async Task UnexpectedCompletion_FailsWithoutHotLoop()
    {
        var lease = new TestLease();
        var factory = new TestLockFactory((_, _) => Task.FromResult<IRedLock>(lease));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRunner(factory).RunAsync("worker", _ => Task.CompletedTask, TestContext.Current.CancellationToken));
        Assert.True(lease.Disposed);
        Assert.Equal(1, factory.Attempts);
    }

    [Fact]
    public async Task CleanupFault_PropagatesWithoutReacquisition()
    {
        using var stopping = new CancellationTokenSource();
        var expected = new InvalidOperationException("cleanup failed");
        var lease = new TestLease();
        var started = Signal();
        var factory = new TestLockFactory((_, _) => Task.FromResult<IRedLock>(lease));
        var running = CreateRunner(factory).RunAsync("worker", async token =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally
            {
                throw expected;
            }
        }, stopping.Token);
        await started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        lease.Lose();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            running.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
        Assert.True(lease.Disposed);
        Assert.Equal(1, factory.Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquisitionFault_PropagatesWithoutStartingWork(bool synchronous)
    {
        var expected = new IOException("Synthetic acquisition failure.");
        var factory = new TestLockFactory((_, _) => synchronous
            ? throw expected : Task.FromException<IRedLock>(expected));
        var executions = 0;

        var actual = await Assert.ThrowsAsync<IOException>(() =>
            CreateRunner(factory).RunAsync("worker", _ =>
            {
                executions++;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal(0, executions);
        Assert.Equal(1, factory.Attempts);
    }

    [Fact]
    public async Task ReleaseFault_PreservesInfrastructureFailureIdentity()
    {
        var expected = new IOException("Synthetic release failure.");
        var lease = new TestLease { DisposalFault = expected };
        var factory = new TestLockFactory((_, _) => Task.FromResult<IRedLock>(lease));

        var actual = await Assert.ThrowsAsync<IOException>(() =>
            CreateRunner(factory).RunAsync("worker",
                _ => Task.FromException(new IOException("Synthetic worker failure.")),
                TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.True(lease.Disposed);
        Assert.Equal(1, factory.Attempts);
    }

    [Fact]
    public async Task ObservationFault_CancelsAndJoinsWorkerBeforeRelease()
    {
        var expected = new IOException("Synthetic observation failure.");
        var lease = new TestLease();
        var started = Signal();
        var cleaning = Signal();
        var finishCleanup = Signal();
        var factory = new TestLockFactory((_, _) => Task.FromResult<IRedLock>(lease));
        CancellationToken workerToken = default;
        var running = CreateRunner(factory).RunAsync("worker", async token =>
        {
            workerToken = token;
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
        }, TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        lease.FailObservation(expected);
        try
        {
            await cleaning.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Assert.True(workerToken.IsCancellationRequested);
            Assert.False(lease.Disposed);
            Assert.False(running.IsCompleted);
        }
        finally
        {
            finishCleanup.TrySetResult();
        }
        var actual = await Assert.ThrowsAsync<IOException>(() =>
            running.WaitAsync(_timeout, TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.True(lease.Disposed);
        Assert.Equal(1, factory.Attempts);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static LeadershipRunner CreateRunner(IDistributedLockFactory factory)
        => new(NullLogger<LeadershipRunner>.Instance, Options.Create(new RedlockConfig
        {
            LeadershipCheckIntervalMs = 1,
            Profiles = new()
            {
                [RedlockProfiles.LeaderElection] = new() { ExpiryMs = 30_000, WaitMs = 60_000, RetryMs = 1 }
            }
        }), TimeProvider.System, factory);
}
