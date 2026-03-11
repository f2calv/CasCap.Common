namespace CasCap.Common.Xunit;

/// <summary>
/// Skips the test when running in any CI environment (Azure DevOps or GitHub Actions).
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfCIBuildFactAttribute : FactAttribute
{
    /// <summary>Initializes a new instance of the <see cref="SkipIfCIBuildFactAttribute"/> class.</summary>
    public SkipIfCIBuildFactAttribute()
    {
        if (IsCI())
            Skip = "Ignore test when running a CI build";
    }

    private static bool IsCI() => Environment.GetEnvironmentVariable("TF_BUILD") is not null
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}
