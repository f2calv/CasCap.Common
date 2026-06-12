namespace CasCap.Common.Xunit;

/// <summary>Exposes the current continuous-integration environment as static boolean properties.</summary>
/// <remarks>Referenced by the <c>SkipIf*</c> attributes via xUnit v3's <see cref="Xunit.FactAttribute.SkipWhen"/> / <see cref="Xunit.FactAttribute.SkipType"/> conditional-skip mechanism.</remarks>
[ExcludeFromCodeCoverage]
public static class CIEnvironment
{
    /// <summary>True when running under an Azure DevOps build (the <c>TF_BUILD</c> environment variable is set).</summary>
    public static bool IsAzureDevOps => Environment.GetEnvironmentVariable("TF_BUILD") is not null;

    /// <summary>True when running under a GitHub Actions build (the <c>GITHUB_ACTIONS</c> environment variable is set).</summary>
    public static bool IsGitHubActions => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;

    /// <summary>True when running under any supported CI environment (Azure DevOps or GitHub Actions).</summary>
    public static bool IsCI => IsAzureDevOps || IsGitHubActions;
}
