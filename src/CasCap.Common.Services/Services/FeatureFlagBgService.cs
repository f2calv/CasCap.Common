namespace CasCap.Common.Services;

/// <summary>
/// <see cref="BackgroundService"/> that resolves all registered <see cref="IBgFeature"/>
/// implementations and launches those whose <see cref="IBgFeature.FeatureName"/> is present
/// in the configured <see cref="FeatureFlagConfig.EnabledFeatures"/> set.
/// </summary>
/// <remarks>
/// Features with <see cref="IBgFeature.FeatureName"/> equal to <see cref="IBgFeature.AlwaysEnabled"/>
/// are launched regardless of the enabled set. A run-to-completion feature ends the process by calling
/// <see cref="IHostApplicationLifetime.StopApplication"/> before returning; any other completion of every
/// enabled feature is treated as a fault.
/// </remarks>
public sealed partial class FeatureFlagBgService(
    ILogger<FeatureFlagBgService> logger,
    IOptions<FeatureFlagConfig> featureConfig,
    IEnumerable<IBgFeature> features,
    IHostApplicationLifetime appLifetime) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        LogStarting(logger, nameof(FeatureFlagBgService));
        using var featureCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, appLifetime.ApplicationStopping);
        var runningFeatures = new List<(string Name, Task Task)>(features.Count());
        try
        {
            foreach (var feature in features)
            {
                if (string.Equals(feature.FeatureName, IBgFeature.AlwaysEnabled, StringComparison.OrdinalIgnoreCase)
                    || featureConfig.Value.EnabledFeatures.Contains(feature.FeatureName))
                {
                    LogFeatureStarting(logger, nameof(FeatureFlagBgService), feature.GetType().Name);
                    runningFeatures.Add((feature.FeatureName, feature.ExecuteAsync(featureCancellation.Token)));
                }
            }
            if (runningFeatures.IsNullOrEmpty())
                throw new GenericException("no features found to launch!");

            var startedFeatureNames = runningFeatures.Select(feature => feature.Name).ToArray();
            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, featureCancellation.Token);
            while (runningFeatures.Count > 0 && !featureCancellation.IsCancellationRequested)
            {
                var completedTask = await Task.WhenAny(
                    runningFeatures.Select(feature => feature.Task).Append(cancellationTask)).ConfigureAwait(false);
                if (ReferenceEquals(completedTask, cancellationTask) || featureCancellation.IsCancellationRequested)
                    break;

                var completedFeatureIndex = runningFeatures.FindIndex(feature => ReferenceEquals(feature.Task, completedTask));
                var completedFeature = runningFeatures[completedFeatureIndex];
                await completedFeature.Task.ConfigureAwait(false);
                runningFeatures.RemoveAt(completedFeatureIndex);
                LogFeatureCompleted(logger, nameof(FeatureFlagBgService), completedFeature.Name);
            }

            if (!featureCancellation.IsCancellationRequested)
                throw new InvalidOperationException(
                    $"All enabled background features completed before host cancellation: {string.Join(", ", startedFeatureNames)}.");
        }
        catch
        {
            appLifetime.StopApplication();
            throw;
        }
        finally
        {
            try
            {
                await featureCancellation.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await Task.WhenAll(runningFeatures.Select(feature => feature.Task)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (featureCancellation.IsCancellationRequested) { }
            }
        }

        LogExiting(logger, nameof(FeatureFlagBgService));
    }

    [LoggerMessage(LogLevel.Information, "{ClassName} starting")]
    private static partial void LogStarting(ILogger logger, string className);

    [LoggerMessage(LogLevel.Information, "{ClassName} starting {FeatureName}")]
    private static partial void LogFeatureStarting(ILogger logger, string className, string featureName);

    [LoggerMessage(LogLevel.Information, "{ClassName} {FeatureName} completed")]
    private static partial void LogFeatureCompleted(ILogger logger, string className, string featureName);

    [LoggerMessage(LogLevel.Information, "{ClassName} exiting")]
    private static partial void LogExiting(ILogger logger, string className);
}
