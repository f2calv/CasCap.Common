namespace CasCap.Common.Xunit;

/// <summary>Skips the test when running in an Azure DevOps build.</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfAzureDevOpsBuildFactAttribute : FactAttribute
{
    /// <summary>Initialises a new instance of the <see cref="SkipIfAzureDevOpsBuildFactAttribute"/> class.</summary>
    public SkipIfAzureDevOpsBuildFactAttribute()
    {
        Skip = "Ignore test when running an Azure DevOps build";
        SkipWhen = nameof(CIEnvironment.IsAzureDevOps);
        SkipType = typeof(CIEnvironment);
    }
}
