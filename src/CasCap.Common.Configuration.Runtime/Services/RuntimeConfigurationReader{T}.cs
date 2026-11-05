using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Runtime.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasCap.Common.Configuration.Runtime.Services;

/// <summary>Loads a required initial revision and reconciles changes without overlapping polls.</summary>
internal sealed partial class RuntimeConfigurationReader<T>(
    ILogger<RuntimeConfigurationReader<T>> logger,
    TimeProvider timeProvider,
    RuntimeConfigurationOptions settings,
    IRuntimeConfigurationStore<T> store,
    RuntimeConfigurationCodec<T> codec,
    RuntimeSnapshotProvider provider,
    RuntimeConfigurationBaseline<T> baseline
) : BackgroundService where T : class
{
    private long _revision;
    private DateTimeOffset? _expiresAtUtc;
    private long _expiryReportedRevision;
    private int _consecutiveFailures;
    private DateTimeOffset _failingSince;

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(settings.StartupTimeout);
        await RefreshAsync(deadline.Token);
        await base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(settings.RefreshInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshAsync(stoppingToken);
                OnRefreshSucceeded();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                OnRefreshFailed(exception);
                ApplyAcceptedExpiry();
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ApplyAcceptedExpiry();
        var revision = await store.ReadAsync(cancellationToken)
            ?? throw new InvalidOperationException("A required runtime configuration revision has not been published.");
        if (revision.Revision < _revision)
            throw new InvalidOperationException("The durable runtime configuration revision regressed.");
        var expired = revision.ExpiresAtUtc <= timeProvider.GetUtcNow();
        var json = expired ? baseline.Json : codec.Serialize(revision.Value);
        provider.Publish(json);
        var activated = revision.Revision != _revision;
        _revision = revision.Revision;
        _expiresAtUtc = revision.ExpiresAtUtc;
        if (!activated)
            return;
        if (expired)
            _expiryReportedRevision = revision.Revision;
        LogRevisionActivated(logger, nameof(RuntimeConfigurationReader<>), revision.Revision, expired);
    }

    private void ApplyAcceptedExpiry()
    {
        if (!(_expiresAtUtc <= timeProvider.GetUtcNow()))
            return;
        provider.Publish(baseline.Json);
        if (_expiryReportedRevision == _revision)
            return;
        _expiryReportedRevision = _revision;
        LogRevisionExpired(logger, nameof(RuntimeConfigurationReader<>), _revision);
    }

    private void OnRefreshFailed(Exception exception)
    {
        // Store and validation errors can contain values; never export the exception payload.
        if (_consecutiveFailures++ == 0)
        {
            _failingSince = timeProvider.GetUtcNow();
            LogRefreshFailed(logger, nameof(RuntimeConfigurationReader<>), exception.GetType().Name);
        }
        else
            LogRefreshStillFailing(logger, nameof(RuntimeConfigurationReader<>), exception.GetType().Name, _consecutiveFailures);
    }

    private void OnRefreshSucceeded()
    {
        if (_consecutiveFailures == 0)
            return;
        LogRefreshRecovered(logger, nameof(RuntimeConfigurationReader<>), _consecutiveFailures, timeProvider.GetUtcNow() - _failingSince);
        _consecutiveFailures = 0;
    }

    [LoggerMessage(LogLevel.Information, "{ClassName} activated Revision={Revision} Expired={Expired}")]
    private static partial void LogRevisionActivated(ILogger logger, string className, long revision, bool expired);

    [LoggerMessage(LogLevel.Information, "{ClassName} Revision={Revision} expired; baseline restored")]
    private static partial void LogRevisionExpired(ILogger logger, string className, long revision);

    [LoggerMessage(LogLevel.Error, "{ClassName} refresh failed ({FailureType}); inspect store validation and reload subscribers")]
    private static partial void LogRefreshFailed(ILogger logger, string className, string failureType);

    [LoggerMessage(LogLevel.Debug, "{ClassName} refresh still failing ({FailureType}) ConsecutiveFailures={ConsecutiveFailures}")]
    private static partial void LogRefreshStillFailing(ILogger logger, string className, string failureType, int consecutiveFailures);

    [LoggerMessage(LogLevel.Information, "{ClassName} refresh recovered ConsecutiveFailures={ConsecutiveFailures} Downtime={Downtime}")]
    private static partial void LogRefreshRecovered(ILogger logger, string className, int consecutiveFailures, TimeSpan downtime);
}
