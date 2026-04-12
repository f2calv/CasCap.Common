namespace CasCap.Common.Services;

/// <summary>
/// This service acts as a flexible background service launcher for multiple services that implement <see cref="IFeature{T}"/>.
/// </summary>
public class FeatureFlagBgService<T> : BackgroundService
    where T : Enum
{
    private readonly ILogger _logger;
    private readonly IFeatureConfig<T> _featureOptions;
    private readonly IEnumerable<IFeature<T>> _features;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagBgService{T}"/> class.
    /// </summary>
    public FeatureFlagBgService(ILogger<FeatureFlagBgService<T>> logger, IOptions<FeatureConfig<T>> featureOptions, IEnumerable<IFeature<T>> features)
    {
        _logger = logger;
        _featureOptions = featureOptions.Value;
        _features = features;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _logger.LogInformation("{ClassName} starting", nameof(FeatureFlagBgService<T>));
        var tasks = new List<Task>(_features.Count());
        foreach (var feature in _features)
        {
            if (_featureOptions.AppMode.HasFlag(feature.FeatureType))
            {
                _logger.LogInformation("{ClassName} starting {FeatureName}",
                    nameof(FeatureFlagBgService<T>), feature.GetType().Name);
                tasks.Add(feature.ExecuteAsync(stoppingToken));
            }
        }
        if (tasks.IsNullOrEmpty())
            throw new GenericException("no features found to launch!");
        //await-await-WhenAny propagates the first faulted task immediately so the
        //service crashes and the pod restarts rather than running in a degraded state.
        await await Task.WhenAny(tasks);
        _logger.LogInformation("{ClassName} exiting", nameof(FeatureFlagBgService<T>));
    }
}
