namespace CasCap.Common.Services;

/// <summary>
/// <see cref="BackgroundService"/> that resolves all registered <see cref="IBgFeature"/>
/// implementations and launches those whose <see cref="IBgFeature.FeatureName"/> is present
/// in the configured <see cref="FeatureFlagConfig.EnabledFeatures"/> set.
/// </summary>
/// <remarks>
/// Features with <see cref="IBgFeature.FeatureName"/> equal to <see cref="IBgFeature.AlwaysEnabled"/>
/// are launched regardless of the enabled set.
/// </remarks>
public sealed class FeatureFlagBgService(ILogger<FeatureFlagBgService> logger, IOptions<FeatureFlagConfig> featureConfig, IEnumerable<IBgFeature> features) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("{ClassName} starting", nameof(FeatureFlagBgService));
        var runningFeatures = new List<(string Name, Task Task)>(features.Count());
        foreach (var feature in features)
        {
            if (string.Equals(feature.FeatureName, IBgFeature.AlwaysEnabled, StringComparison.OrdinalIgnoreCase)
                || featureConfig.Value.EnabledFeatures.Contains(feature.FeatureName))
            {
                logger.LogInformation("{ClassName} starting {FeatureName}",
                    nameof(FeatureFlagBgService), feature.GetType().Name);
                runningFeatures.Add((feature.FeatureName, feature.ExecuteAsync(stoppingToken)));
            }
        }
        if (runningFeatures.IsNullOrEmpty())
            throw new GenericException("no features found to launch!");

        var startedFeatureNames = runningFeatures.Select(feature => feature.Name).ToArray();
        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        while (runningFeatures.Count > 0 && !stoppingToken.IsCancellationRequested)
        {
            var completedTask = await Task.WhenAny(
                runningFeatures.Select(feature => feature.Task).Append(cancellationTask)).ConfigureAwait(false);
            if (ReferenceEquals(completedTask, cancellationTask) || stoppingToken.IsCancellationRequested)
                break;

            var completedFeatureIndex = runningFeatures.FindIndex(feature => ReferenceEquals(feature.Task, completedTask));
            var completedFeature = runningFeatures[completedFeatureIndex];
            await completedFeature.Task.ConfigureAwait(false);
            runningFeatures.RemoveAt(completedFeatureIndex);
            logger.LogInformation("{ClassName} {FeatureName} completed",
                nameof(FeatureFlagBgService), completedFeature.Name);
        }

        if (!stoppingToken.IsCancellationRequested)
            throw new InvalidOperationException(
                $"All enabled background features completed before host cancellation: {string.Join(", ", startedFeatureNames)}.");

        logger.LogInformation("{ClassName} exiting", nameof(FeatureFlagBgService));
    }
}
