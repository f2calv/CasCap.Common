using System;
using Xunit;
namespace CasCap.Common.Testing
{
    public sealed class SkipIfAzureDevOpsBuildFact : FactAttribute
    {
        public SkipIfAzureDevOpsBuildFact()
        {
            if (IsAzureDevOps())
                Skip = "Ignore test when running an Azure DevOps build";
        }

        static bool IsAzureDevOps() => Environment.GetEnvironmentVariable("TF_BUILD") is not null;
    }
}