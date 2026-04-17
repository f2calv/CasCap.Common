namespace CasCap.Common.Services;

/// <summary>Dumps build information to the log to aid debugging.</summary>
/// <remarks>
/// Registered as a hosted service by <see cref="ServiceCollectionExtensions.AddFeatureFlagService(IReadOnlySet{string}, bool)"/>
/// when <c>addGitMetadataService</c> is <see langword="true"/>.
/// </remarks>
public class GitMetadataBgService(ILogger<GitMetadataBgService> logger, GitMetadata gitMetadata) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("{ClassName} starting", nameof(GitMetadataBgService));
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("{ClassName} GIT_REPOSITORY {GIT_REPOSITORY}, GIT_TAG {GIT_TAG}, GIT_BRANCH {GIT_BRANCH}, GIT_COMMIT {GIT_COMMIT}",
                nameof(GitMetadataBgService),
                gitMetadata.GIT_REPOSITORY,
                gitMetadata.GIT_TAG,
                gitMetadata.GIT_BRANCH,
                gitMetadata.GIT_COMMIT
                );
            await Task.Delay(60_000, stoppingToken);
        }
        logger.LogInformation("{ClassName} exiting", nameof(GitMetadataBgService));
    }
}
