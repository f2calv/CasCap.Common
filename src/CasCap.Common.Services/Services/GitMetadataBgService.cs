namespace CasCap.Common.Services;

/// <summary>Dumps build information to the log to aid debugging.</summary>
/// <remarks>
/// Registered as a hosted service by <see cref="ServiceCollectionExtensions.AddFeatureFlagService(IReadOnlySet{string}, bool)"/>
/// when <c>addGitMetadataService</c> is <see langword="true"/>.
/// </remarks>
public sealed partial class GitMetadataBgService(ILogger<GitMetadataBgService> logger, GitMetadata gitMetadata) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        LogStarting(logger, nameof(GitMetadataBgService));
        while (!stoppingToken.IsCancellationRequested)
        {
            LogMetadata(
                logger,
                nameof(GitMetadataBgService),
                gitMetadata.GIT_REPOSITORY,
                gitMetadata.GIT_TAG,
                gitMetadata.GIT_BRANCH,
                gitMetadata.GIT_COMMIT);
            await Task.Delay(60_000, stoppingToken).ConfigureAwait(false);
        }
        LogExiting(logger, nameof(GitMetadataBgService));
    }

    [LoggerMessage(LogLevel.Information, "{ClassName} starting")]
    private static partial void LogStarting(ILogger logger, string className);

    [LoggerMessage(LogLevel.Information, "{ClassName} GIT_REPOSITORY {GitRepository}, GIT_TAG {GitTag}, GIT_BRANCH {GitBranch}, GIT_COMMIT {GitCommit}")]
    private static partial void LogMetadata(
        ILogger logger,
        string className,
        string? gitRepository,
        string? gitTag,
        string? gitBranch,
        string? gitCommit);

    [LoggerMessage(LogLevel.Information, "{ClassName} exiting")]
    private static partial void LogExiting(ILogger logger, string className);
}
