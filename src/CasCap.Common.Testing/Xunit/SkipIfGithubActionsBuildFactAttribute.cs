namespace CasCap.Common.Xunit;

/// <summary>
/// Skips the test when running in a GitHub Actions build.
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfGithubActionsBuildFactAttribute : FactAttribute
{
    public SkipIfGithubActionsBuildFactAttribute()
    {
        if (IsGitHubActions())
            Skip = "Ignore test when running a Github Actions build";
    }

    private static bool IsGitHubActions() => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}
