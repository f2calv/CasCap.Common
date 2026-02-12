namespace CasCap.Common.Xunit;

/// <summary>
/// Skips the theory when running in an Azure DevOps build.
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfAzureDevOpsBuildTheoryAttribute : TheoryAttribute
{
    public SkipIfAzureDevOpsBuildTheoryAttribute()
    {
        if (IsAzureDevOps())
            Skip = "Ignore test when running an Azure DevOps build";
    }

    private static bool IsAzureDevOps() => Environment.GetEnvironmentVariable("TF_BUILD") is not null;
}
