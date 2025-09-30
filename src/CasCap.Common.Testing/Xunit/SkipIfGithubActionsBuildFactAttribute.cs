namespace CasCap.Common.Xunit;

[ExcludeFromCodeCoverage]
public sealed class SkipIfGithubActionsBuildFactAttribute : FactAttribute
{
    public SkipIfGithubActionsBuildFactAttribute()
    {
        if (IsGitHubActions())
            Skip = "Ignore test when running a Github Actions build";
    }

    static bool IsGitHubActions() => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}
