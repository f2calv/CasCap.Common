namespace CasCap.Common.Xunit;

[ExcludeFromCodeCoverage]
public sealed class SkipIfAzureDevOpsBuildFactAttribute : FactAttribute
{
    public SkipIfAzureDevOpsBuildFactAttribute()
    {
        if (IsAzureDevOps())
            Skip = "Ignore test when running an Azure DevOps build";
    }

    static bool IsAzureDevOps() => Environment.GetEnvironmentVariable("TF_BUILD") is not null;
}
