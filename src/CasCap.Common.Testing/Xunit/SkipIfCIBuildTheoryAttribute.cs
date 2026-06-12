namespace CasCap.Common.Xunit;

/// <summary>Skips the theory when running in any CI environment (Azure DevOps or GitHub Actions).</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfCIBuildTheoryAttribute : TheoryAttribute
{
    /// <summary>Initialises a new instance of the <see cref="SkipIfCIBuildTheoryAttribute"/> class.</summary>
    public SkipIfCIBuildTheoryAttribute()
    {
        Skip = "Ignore test when running a CI build";
        SkipWhen = nameof(CIEnvironment.IsCI);
        SkipType = typeof(CIEnvironment);
    }
}
