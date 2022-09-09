namespace CasCap.Xunit;

public sealed class SkipIfCIBuildTheory : TheoryAttribute
{
    public SkipIfCIBuildTheory()
    {
        if (IsCI())
            Skip = "Ignore test when running a CI build";
    }

    static bool IsCI() => Environment.GetEnvironmentVariable("TF_BUILD") is not null
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}