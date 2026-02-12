namespace CasCap.Common.Xunit;

/// <summary>
/// Skips the theory when running in any CI environment (Azure DevOps or GitHub Actions).
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfCIBuildTheoryAttribute : TheoryAttribute
{
    public SkipIfCIBuildTheoryAttribute()
    {
        if (IsCI())
            Skip = "Ignore test when running a CI build";
    }

    private static bool IsCI() => Environment.GetEnvironmentVariable("TF_BUILD") is not null
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}
