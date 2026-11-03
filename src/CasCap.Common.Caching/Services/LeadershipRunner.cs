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

        var standby = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var lease = await distributedLockFactory.CreateLockAsync(
                    resource, expiry, wait, retry, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!lease.IsAcquired)
                {
                    if (standby)
                        LogContending(logger, nameof(LeadershipRunner), resource);
                    else
                        LogStandby(logger, nameof(LeadershipRunner), resource);
                    standby = true;
                    await Task.Delay(retry, timeProvider, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                standby = false;
                LogAcquired(logger, nameof(LeadershipRunner), resource);
                await RunOwnedAsync(lease, resource, runAsync, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogStopped(logger, nameof(LeadershipRunner), resource);
        }
    }

    private async Task RunOwnedAsync(IRedLock lease, string resource, Func<CancellationToken, Task> runAsync, CancellationToken cancellationToken)
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
            LogLost(logger, nameof(LeadershipRunner), resource);
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

    [LoggerMessage(LogLevel.Information, "{ClassName} leadership lease for {Resource} held elsewhere; entering standby")]
    private static partial void LogStandby(ILogger logger, string className, string resource);

    [LoggerMessage(LogLevel.Debug, "{ClassName} leadership lease for {Resource} unavailable; remaining on standby")]
    private static partial void LogContending(ILogger logger, string className, string resource);

    [LoggerMessage(LogLevel.Information, "{ClassName} leadership lease for {Resource} acquired; starting protected work")]
    private static partial void LogAcquired(ILogger logger, string className, string resource);

    [LoggerMessage(LogLevel.Warning, "{ClassName} leadership lease for {Resource} lost; cancelling and awaiting protected work before reacquisition")]
    private static partial void LogLost(ILogger logger, string className, string resource);

    [LoggerMessage(LogLevel.Information, "{ClassName} leadership runner for {Resource} stopped after protected work cleanup")]
    private static partial void LogStopped(ILogger logger, string className, string resource);
}
#endif
