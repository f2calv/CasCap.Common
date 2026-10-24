#if NET8_0_OR_GREATER
using RedLockNet;

namespace CasCap.Common.Services;

/// <summary>Coordinates long-lived work through the caching registration's existing Redlock factory and options.</summary>
public sealed partial class LeadershipRunner(
    ILogger<LeadershipRunner> logger,
    IOptions<RedlockConfig> config,
    TimeProvider timeProvider,
    IDistributedLockFactory distributedLockFactory
    ) : ILeadershipRunner
{
    /// <inheritdoc/>
    public async Task RunAsync(string resource, Func<CancellationToken, Task> runAsync, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentNullException.ThrowIfNull(runAsync);
        var (expiry, wait, retry) = config.Value.GetTimings(RedlockProfiles.LeaderElection);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var lease = await distributedLockFactory.CreateLockAsync(
                    resource, expiry, wait, retry, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!lease.IsAcquired)
                {
                    LogContending(logger, nameof(LeadershipRunner));
                    await Task.Delay(retry, timeProvider, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                LogAcquired(logger, nameof(LeadershipRunner));
                await RunOwnedAsync(lease, runAsync, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogStopped(logger, nameof(LeadershipRunner));
        }
    }

    private async Task RunOwnedAsync(IRedLock lease, Func<CancellationToken, Task> runAsync, CancellationToken cancellationToken)
    {
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var worker = runAsync(runCancellation.Token);
        var observation = ObserveLeaseAsync(lease, runCancellation.Token);
        try
        {
            var completed = await Task.WhenAny(worker, observation).ConfigureAwait(false);
            if (ReferenceEquals(completed, worker))
            {
                await worker.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("The leadership worker completed without a stop request.");
            }

            await observation.ConfigureAwait(false);
            LogLost(logger, nameof(LeadershipRunner));
        }
        finally
        {
            try
            {
                await runCancellation.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await worker.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (runCancellation.IsCancellationRequested) { }
                finally
                {
                    try
                    {
                        await observation.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (runCancellation.IsCancellationRequested) { }
                }
            }
        }
    }

    private async Task ObserveLeaseAsync(IRedLock lease, CancellationToken cancellationToken)
    {
        while (lease.IsAcquired)
            await Task.Delay(TimeSpan.FromMilliseconds(config.Value.LeadershipCheckIntervalMs),
                timeProvider, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(LogLevel.Debug, "{ClassName} leadership lease unavailable; remaining on standby")]
    private static partial void LogContending(ILogger logger, string className);

    [LoggerMessage(LogLevel.Information, "{ClassName} leadership lease acquired; starting protected work")]
    private static partial void LogAcquired(ILogger logger, string className);

    [LoggerMessage(LogLevel.Warning, "{ClassName} leadership lease lost; cancelling and awaiting protected work before reacquisition")]
    private static partial void LogLost(ILogger logger, string className);

    [LoggerMessage(LogLevel.Information, "{ClassName} leadership runner stopped after protected work cleanup")]
    private static partial void LogStopped(ILogger logger, string className);
}
#endif
