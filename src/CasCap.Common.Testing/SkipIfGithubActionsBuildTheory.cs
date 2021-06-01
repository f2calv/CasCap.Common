using System;
using Xunit;
namespace CasCap.Common.Testing
{
    public sealed class SkipIfGithubActionsBuildTheory : TheoryAttribute
    {
        public SkipIfGithubActionsBuildTheory()
        {
            if (IsGitHubActions())
                Skip = "Ignore test when running in Github Actions";
        }

        static bool IsGitHubActions() => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
    }
}