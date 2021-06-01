using System;
using Xunit;
namespace CasCap.Common.Testing
{
    public sealed class SkipIfCIBuildFact : FactAttribute
    {
        public SkipIfCIBuildFact()
        {
            if (IsCI())
                Skip = "Ignore test when running a CI build";
        }

        static bool IsCI() => Environment.GetEnvironmentVariable("TF_BUILD") is not null
            || Environment.GetEnvironmentVariable("TF_BUILD") is not null;
    }
}