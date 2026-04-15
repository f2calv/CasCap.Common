namespace CasCap.Common.Xunit;

/// <summary>Skips the test when running in a GitHub Actions build.</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfGithubActionsBuildFactAttribute() : FactAttribute
{
    /// <inheritdoc/>
    public override string? Skip
    {
        get => IsGitHubActions() ? "Ignore test when running a Github Actions build" : base.Skip;
        set => base.Skip = value;
    }

    private static bool IsGitHubActions() => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}
