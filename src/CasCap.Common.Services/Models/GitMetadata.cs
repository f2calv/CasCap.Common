namespace CasCap.Common.Models;

/// <summary>Build metadata from the CI/CD pipeline.</summary>
/// <remarks>Properties bind to environment variables injected by GitHub Actions and Helm deployments.</remarks>
public record GitHub
{
    /// <summary>Source repository name.</summary>
    public string GIT_REPOSITORY { get; init; } = Environment.GetEnvironmentVariable(nameof(GIT_REPOSITORY)) ?? "n/a";

    /// <summary>Git branch name.</summary>
    public string GIT_BRANCH { get; init; } = Environment.GetEnvironmentVariable(nameof(GIT_BRANCH)) ?? "n/a";

    /// <summary>Git commit SHA.</summary>
    public string GIT_COMMIT { get; init; } = Environment.GetEnvironmentVariable(nameof(GIT_COMMIT)) ?? "n/a";

    /// <summary>Git tag.</summary>
    public string GIT_TAG { get; init; } = Environment.GetEnvironmentVariable(nameof(GIT_TAG)) ?? "n/a";

    /// <summary>GitHub Actions workflow name.</summary>
    public string GITHUB_WORKFLOW { get; init; } = Environment.GetEnvironmentVariable(nameof(GITHUB_WORKFLOW)) ?? "n/a";

    /// <summary>GitHub Actions run ID.</summary>
    public string GITHUB_RUN_ID { get; init; } = Environment.GetEnvironmentVariable(nameof(GITHUB_RUN_ID)) ?? "n/a";

    /// <summary>GitHub Actions run number.</summary>
    public string GITHUB_RUN_NUMBER { get; init; } = Environment.GetEnvironmentVariable(nameof(GITHUB_RUN_NUMBER)) ?? "n/a";
}
