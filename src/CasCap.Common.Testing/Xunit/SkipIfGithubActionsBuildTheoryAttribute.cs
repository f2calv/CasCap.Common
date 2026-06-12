namespace CasCap.Common.Xunit;

/// <summary>Skips the theory when running in a GitHub Actions build.</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfGithubActionsBuildTheoryAttribute : TheoryAttribute
{
    /// <summary>Initialises a new instance of the <see cref="SkipIfGithubActionsBuildTheoryAttribute"/> class.</summary>
    public SkipIfGithubActionsBuildTheoryAttribute()
    {
        Skip = "Ignore test when running a Github Actions build";
        SkipWhen = nameof(CIEnvironment.IsGitHubActions);
        SkipType = typeof(CIEnvironment);
    }
}
