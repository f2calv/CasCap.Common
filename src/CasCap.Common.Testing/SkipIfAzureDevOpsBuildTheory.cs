using System;
using Xunit;
namespace CasCap.Common.Testing
{
    public sealed class SkipIfAzureDevOpsBuildTheory : TheoryAttribute
    {
        public SkipIfAzureDevOpsBuildTheory()
        {
            if (IsAzureDevOps())
                Skip = "Ignore test when running in Azure DevOps";
        }

        static bool IsAzureDevOps() => Environment.GetEnvironmentVariable("TF_BUILD") is not null;
    }
}