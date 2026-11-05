namespace CasCap.Common.Services;

/// <summary>Periodically logs application metadata to aid operational debugging.</summary>
/// <remarks>
/// Registered as an always-enabled background feature by <see cref="ServiceCollectionExtensions.AddFeatureFlagService(IReadOnlySet{string}, bool)"/>
/// when <c>addApplicationMetadataService</c> is <see langword="true"/>.
/// </remarks>
public sealed partial class ApplicationMetadataBgService(
    ILogger<ApplicationMetadataBgService> logger,
    IOptions<ApplicationMetadataConfig> config,
    TimeProvider timeProvider,
    ApplicationMetadata applicationMetadata) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => BgFeatureNames.AlwaysEnabled;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        // The supervisor already logs this feature starting, so the first metadata line is the startup event.
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                LogMetadata(
                    logger,
                    nameof(ApplicationMetadataBgService),
                    applicationMetadata.Repository,
                    applicationMetadata.Version,
                    applicationMetadata.Branch,
                    applicationMetadata.Commit);
                await Task.Delay(config.Value.LogInterval, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        LogExiting(logger, nameof(ApplicationMetadataBgService));
    }

    [LoggerMessage(LogLevel.Information, "{ClassName} Repository={Repository} Version={Version} Branch={Branch} Commit={Commit}")]
    private static partial void LogMetadata(
        ILogger logger,
        string className,
        string repository,
        string version,
        string branch,
        string commit);

    [LoggerMessage(LogLevel.Information, "{ClassName} exiting")]
    private static partial void LogExiting(ILogger logger, string className);
}
