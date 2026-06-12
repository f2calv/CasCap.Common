namespace CasCap.Common.Xunit;

/// <summary>Skips the test when running in a GitHub Actions build.</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfGithubActionsBuildFactAttribute : FactAttribute
{
    /// <summary>Initialises a new instance of the <see cref="SkipIfGithubActionsBuildFactAttribute"/> class.</summary>
    public SkipIfGithubActionsBuildFactAttribute()
    {
        Skip = "Ignore test when running a Github Actions build";
        SkipWhen = nameof(CIEnvironment.IsGitHubActions);
        SkipType = typeof(CIEnvironment);
    }
}
