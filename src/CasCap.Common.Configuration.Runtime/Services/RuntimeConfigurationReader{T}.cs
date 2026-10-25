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
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Store and validation errors can contain values; never export the exception payload.
                LogRefreshFailed(logger, nameof(RuntimeConfigurationReader<>), exception.GetType().Name);
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var revision = await store.ReadAsync(cancellationToken)
            ?? throw new InvalidOperationException("A required runtime configuration revision has not been published.");
        if (revision.Revision < _revision)
            throw new InvalidOperationException("The durable runtime configuration revision regressed.");
        var json = revision.ExpiresAtUtc <= timeProvider.GetUtcNow()
            ? baseline.Json : codec.Serialize(revision.Value);
        provider.Publish(json);
        _revision = revision.Revision;
    }

    [LoggerMessage(LogLevel.Error, "{ClassName} refresh failed ({FailureType}); inspect store validation and reload subscribers")]
    private static partial void LogRefreshFailed(ILogger logger, string className, string failureType);
}
