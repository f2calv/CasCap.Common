namespace CasCap.Common.Services;

/// <summary>Dumps build information to the log to aid debugging.</summary>
/// <remarks>
/// Always runs regardless of the active feature flags because
/// <see cref="IFeature{T}.FeatureType"/> returns the zero value of <typeparamref name="T"/>
/// which satisfies <see cref="Enum.HasFlag"/> for every flag combination.
/// </remarks>
public class MetadataBgService<T>(ILogger<MetadataBgService<T>> logger, GitHub gitHub) : IFeature<T>
    where T : Enum
{
    /// <inheritdoc/>
    public T FeatureType => default!;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(MetadataBgService<T>));
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(MetadataBgService<T>));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("{ClassName} GIT_REPOSITORY {GIT_REPOSITORY}, GIT_TAG {GIT_TAG}, GIT_BRANCH {GIT_BRANCH}, GIT_COMMIT {GIT_COMMIT}",
                nameof(MetadataBgService<T>),
                gitHub.GIT_REPOSITORY,
                gitHub.GIT_TAG,
                gitHub.GIT_BRANCH,
                gitHub.GIT_COMMIT
                );
            await Task.Delay(60_000, cancellationToken);
        }
    }
}
