using CasCap.Common.Services;
using Cronos;
using Microsoft.Extensions.Time.Testing;

namespace CasCap.Common.Services.Tests;

/// <summary>Verifies UTC occurrence selection, sequential coalescing and awaited cooperative cancellation.</summary>
[Trait("Category", "Scheduling")]
public sealed class CoalescingScheduleRunnerTests
{
    [Theory]
    [InlineData("0 * * * *", 60)]
    [InlineData("*/5 * * * *", 5)]
    public void NextOccurrence(string expression, int minutes)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var cron = CronExpression.Parse(expression);
        Assert.Equal(start.AddMinutes(minutes), CoalescingScheduleRunner.GetNextOccurrence(cron, start));
        Assert.Equal(start.AddMinutes(minutes * 2),
            CoalescingScheduleRunner.GetNextOccurrence(cron, start.AddMinutes(minutes)));
    }

    [Fact]
    public async Task SlowCallbacksCoalesceWithoutOverlap()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(start);
        var observedClock = new RecordingClock(clock);
        using var stop = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var occurrences = new List<DateTimeOffset>();
        var running = CoalescingScheduleRunner.RunAsync(CronExpression.Parse("*/5 * * * *"), observedClock,
            async (scheduled, token) =>
            {
                occurrences.Add(scheduled);
                if (occurrences.Count == 1)
                {
                    entered.SetResult();
                    await release.Task.WaitAsync(token);
                }
                else
                {
                    second.SetResult();
                    await stop.CancelAsync();
                }
            }, stop.Token);
        clock.Advance(TimeSpan.FromMinutes(5));
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Single(occurrences);
        release.SetResult();
        await observedClock.SecondTimer.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(4));
        await second.Task.WaitAsync(TestContext.Current.CancellationToken);
        await running;
        Assert.Equal([start.AddMinutes(5), start.AddMinutes(25)], occurrences);
    }

    [Fact]
    public async Task PendingDelayCancelsWithoutInvokingCallback()
    {
        using var stop = new CancellationTokenSource();
        var count = 0;
        var running = CoalescingScheduleRunner.RunAsync(CronExpression.Parse("0 0 1 1 *"),
            new FakeTimeProvider(), (_, _) =>
            {
                count++;
                return Task.CompletedTask;
            }, stop.Token);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CallbackFaultPropagates()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var error = new InvalidOperationException("Synthetic callback failure.");
        var running = CoalescingScheduleRunner.RunAsync(CronExpression.Parse("* * * * *"), clock,
            (_, _) => Task.FromException(error), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => running));
    }

    [Fact]
    public async Task TimeoutCancelsAndAwaitsOperation()
    {
        var clock = new FakeTimeProvider();
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var running = CoalescingScheduleRunner.RunWithTimeoutAsync(token =>
        {
            observed = token;
            return release.Task;
        }, TimeSpan.FromSeconds(30), clock, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(observed.IsCancellationRequested);
        Assert.False(running.IsCompleted);
        release.SetResult(42);
        await Assert.ThrowsAsync<TimeoutException>(() => running);
    }

    [Fact]
    public async Task CallerCancellationIsNotTimeout()
    {
        using var stop = new CancellationTokenSource();
        var clock = new FakeTimeProvider();
        var running = CoalescingScheduleRunner.RunWithTimeoutAsync(
            async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, clock, token);
                return 1;
            }, TimeSpan.FromMinutes(1), clock, stop.Token);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    private sealed class RecordingClock(FakeTimeProvider clock) : TimeProvider
    {
        private readonly TaskCompletionSource _secondTimer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _timers;

        public Task SecondTimer => _secondTimer.Task;

        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = clock.CreateTimer(callback, state, dueTime, period);
            if (Interlocked.Increment(ref _timers) == 2)
                _secondTimer.SetResult();
            return timer;
        }
    }
}
