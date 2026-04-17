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
public class FeatureFlagBgService(ILogger<FeatureFlagBgService> logger, IOptions<FeatureFlagConfig> featureConfig, IEnumerable<IBgFeature> features) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("{ClassName} starting", nameof(FeatureFlagBgService));
        var tasks = new List<Task>(features.Count());
        foreach (var feature in features)
        {
            if (string.Equals(feature.FeatureName, IBgFeature.AlwaysEnabled, StringComparison.OrdinalIgnoreCase)
                || featureConfig.Value.EnabledFeatures.Contains(feature.FeatureName))
            {
                logger.LogInformation("{ClassName} starting {FeatureName}",
                    nameof(FeatureFlagBgService), feature.GetType().Name);
                tasks.Add(feature.ExecuteAsync(stoppingToken));
            }
        }
        if (tasks.IsNullOrEmpty())
            throw new GenericException("no features found to launch!");
        //await-await-WhenAny propagates the first faulted task immediately so the
        //service crashes and the pod restarts rather than running in a degraded state.
        await await Task.WhenAny(tasks);
        logger.LogInformation("{ClassName} exiting", nameof(FeatureFlagBgService));
    }
}
