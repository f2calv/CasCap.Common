namespace CasCap.Common.Services;

/// <summary>
/// This service acts as a central launcher service for multiple other services which implement <see cref="IFeature"/>.
/// </summary>
public class FeatureFlagBgService<T> : BackgroundService
    where T : Enum
{
    private readonly ILogger _logger;
    private readonly IFeatureOptions<T> _featureOptions;
    private readonly IEnumerable<IFeature<T>> _features;

    public FeatureFlagBgService(ILogger<FeatureFlagBgService<T>> logger, IOptions<FeatureOptions<T>> featureOptions, IEnumerable<IFeature<T>> features)
    {
        _logger = logger;
        _featureOptions = featureOptions.Value;
        _features = features;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _logger.LogInformation("{ClassName} starting", nameof(FeatureFlagBgService<T>));
        var tasks = new List<Task>(_features.Count());
        foreach (var feature in _features)
        {
            //TODO: logging of the start/stop of the service
            //var x = feature.GetType().Name;
            if (_featureOptions.AppMode.HasFlag(feature.FeatureType))
                tasks.Add(feature.ExecuteAsync(stoppingToken));
        }
        if (tasks.IsNullOrEmpty())
            throw new GenericException("no features found to launch!");
        await Task.WhenAll(tasks);
        _logger.LogInformation("{ClassName} exiting", nameof(FeatureFlagBgService<T>));
    }
}
