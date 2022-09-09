namespace CasCap.Xunit;

public sealed class SkipIfAzureDevOpsBuildTheory : TheoryAttribute
{
    public SkipIfAzureDevOpsBuildTheory()
    {
        if (IsAzureDevOps())
            Skip = "Ignore test when running an Azure DevOps build";
    }

    static bool IsAzureDevOps() => Environment.GetEnvironmentVariable("TF_BUILD") is not null;
}