namespace CasCap.Common.Xunit;

/// <summary>Skips the theory when running in an Azure DevOps build.</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfAzureDevOpsBuildTheoryAttribute : TheoryAttribute
{
    /// <summary>Initialises a new instance of the <see cref="SkipIfAzureDevOpsBuildTheoryAttribute"/> class.</summary>
    public SkipIfAzureDevOpsBuildTheoryAttribute()
    {
        Skip = "Ignore test when running an Azure DevOps build";
        SkipWhen = nameof(CIEnvironment.IsAzureDevOps);
        SkipType = typeof(CIEnvironment);
    }
}
