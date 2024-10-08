﻿namespace CasCap.Xunit;

[ExcludeFromCodeCoverage]
public sealed class SkipIfGitHubActionsBuildFact : FactAttribute
{
    public SkipIfGitHubActionsBuildFact()
    {
        if (IsGitHubActions())
            Skip = "Ignore test when running a Github Actions build";
    }

    static bool IsGitHubActions() => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}
