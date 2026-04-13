namespace CasCap.Common.Xunit;

/// <summary>
/// Skips the test when running in any CI environment (Azure DevOps or GitHub Actions).
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfCIBuildFactAttribute() : FactAttribute
{
    /// <inheritdoc/>
    public override string? Skip
    {
        get => IsCI() ? "Ignore test when running a CI build" : base.Skip;
        set => base.Skip = value;
    }

    private static bool IsCI() => Environment.GetEnvironmentVariable("TF_BUILD") is not null
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}
