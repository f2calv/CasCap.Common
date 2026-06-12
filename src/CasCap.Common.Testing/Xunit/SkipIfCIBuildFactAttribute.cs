namespace CasCap.Common.Xunit;

/// <summary>Skips the test when running in any CI environment (Azure DevOps or GitHub Actions).</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfCIBuildFactAttribute : FactAttribute
{
    /// <summary>Initialises a new instance of the <see cref="SkipIfCIBuildFactAttribute"/> class.</summary>
    public SkipIfCIBuildFactAttribute()
    {
        Skip = "Ignore test when running a CI build";
        SkipWhen = nameof(CIEnvironment.IsCI);
        SkipType = typeof(CIEnvironment);
    }
}
