namespace CasCap.Common.Services;

/// <summary>
/// Generic <see cref="BackgroundService"/> that resolves all registered <see cref="IFeature{T}"/>
/// implementations and launches those whose <see cref="IFeature{T}.FeatureType"/> is present
/// in the configured <see cref="IFeatureConfig{T}.EnabledFeatures"/> bitmask.
/// </summary>
public class FeatureFlagBgService<T>(ILogger<FeatureFlagBgService<T>> logger, IOptions<FeatureConfig<T>> featureOptions, IEnumerable<IFeature<T>> features) : BackgroundService
    where T : Enum
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("{ClassName} starting", nameof(FeatureFlagBgService<T>));
        var tasks = new List<Task>(features.Count());
        foreach (var feature in features)
        {
            if (featureOptions.Value.EnabledFeatures.HasFlag(feature.FeatureType))
            {
                logger.LogInformation("{ClassName} starting {FeatureName}",
                    nameof(FeatureFlagBgService<T>), feature.GetType().Name);
                tasks.Add(feature.ExecuteAsync(stoppingToken));
            }
        }
        if (tasks.IsNullOrEmpty())
            throw new GenericException("no features found to launch!");
        //await-await-WhenAny propagates the first faulted task immediately so the
        //service crashes and the pod restarts rather than running in a degraded state.
        await await Task.WhenAny(tasks);
        logger.LogInformation("{ClassName} exiting", nameof(FeatureFlagBgService<T>));
    }
}
