using Cronos;

namespace CasCap.Common.Services;

/// <summary>Runs one sequential UTC Cronos schedule and coalesces missed occurrences after each completed run.</summary>
public static class CoalescingScheduleRunner
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(30);

    /// <summary>Runs scheduled callbacks until cancellation, with at most one callback active at a time.</summary>
    public static async Task RunAsync(
        CronExpression cron,
        TimeProvider timeProvider,
        Func<DateTimeOffset, CancellationToken, Task> callback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cron);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(callback);
        var searchCursor = timeProvider.GetUtcNow();
        while (!cancellationToken.IsCancellationRequested)
        {
            var scheduledForUtc = GetNextOccurrence(cron, searchCursor);
            if (scheduledForUtc is null)
                return;

            await DelayUntilAsync(scheduledForUtc.Value, timeProvider, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await callback(scheduledForUtc.Value, cancellationToken).ConfigureAwait(false);
            var completedAtUtc = timeProvider.GetUtcNow();
            searchCursor = completedAtUtc > scheduledForUtc.Value
                ? completedAtUtc
                : scheduledForUtc.Value;
        }
    }

    /// <summary>Runs a cooperative operation with a timeout driven by the injected clock.</summary>
    public static async Task<T> RunWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timer = timeProvider.CreateTimer(
            static state => ((CancellationTokenSource)state!).Cancel(),
            timeoutSource,
            timeout,
            Timeout.InfiniteTimeSpan);
        try
        {
            var result = await operation(timeoutSource.Token).ConfigureAwait(false);
            return !cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested
                ? throw new TimeoutException($"The scheduled operation exceeded its {timeout.TotalMilliseconds:0} ms timeout.")
                : result;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException($"The scheduled operation exceeded its {timeout.TotalMilliseconds:0} ms timeout.", ex);
        }
    }

    /// <summary>Gets the first UTC occurrence strictly after the supplied search cursor.</summary>
    public static DateTimeOffset? GetNextOccurrence(CronExpression cron, DateTimeOffset searchCursor)
        => cron.GetNextOccurrence(searchCursor, TimeZoneInfo.Utc);

    private static async Task DelayUntilAsync(
        DateTimeOffset scheduledForUtc,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        while (scheduledForUtc - timeProvider.GetUtcNow() is { } delay && delay > TimeSpan.Zero)
            await Task.Delay(delay > MaxDelay ? MaxDelay : delay, timeProvider, cancellationToken).ConfigureAwait(false);
    }
}
