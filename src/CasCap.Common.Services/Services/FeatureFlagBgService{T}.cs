namespace CasCap.Common.Services;

/// <summary>
/// Generic <see cref="BackgroundService"/> that resolves all registered <see cref="IFeature{T}"/>
/// implementations and launches those whose <see cref="IFeature{T}.FeatureType"/> is present
/// in the configured <see cref="IFeatureConfig{T}.EnabledFeatures"/> bitmask.
/// </summary>
[Obsolete("Use the non-generic FeatureFlagBgService with string-based feature names instead.")]
public sealed partial class FeatureFlagBgService<T>(ILogger<FeatureFlagBgService<T>> logger, IOptions<FeatureConfig<T>> featureOptions, IEnumerable<IFeature<T>> features) : BackgroundService
    where T : Enum
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        LogStarting(logger, nameof(FeatureFlagBgService<>));
        var tasks = new List<Task>(features.Count());
        foreach (var feature in features)
        {
            if (featureOptions.Value.EnabledFeatures.HasFlag(feature.FeatureType))
            {
                LogFeatureStarting(logger, nameof(FeatureFlagBgService<>), feature.GetType().Name);
                tasks.Add(feature.ExecuteAsync(stoppingToken));
            }
        }
        if (tasks.IsNullOrEmpty())
            throw new GenericException("no features found to launch!");
        //await-await-WhenAny propagates the first faulted task immediately so the
        //service crashes and the pod restarts rather than running in a degraded state.
        await await Task.WhenAny(tasks);
        LogExiting(logger, nameof(FeatureFlagBgService<>));
    }

    [LoggerMessage(LogLevel.Information, "{ClassName} starting")]
    private static partial void LogStarting(ILogger logger, string className);

    [LoggerMessage(LogLevel.Information, "{ClassName} starting {FeatureName}")]
    private static partial void LogFeatureStarting(ILogger logger, string className, string featureName);

    [LoggerMessage(LogLevel.Information, "{ClassName} exiting")]
    private static partial void LogExiting(ILogger logger, string className);
}
