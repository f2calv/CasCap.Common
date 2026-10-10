using System.Reflection;

namespace CasCap.Common.Models;

/// <summary>Identity and build context for the running application artifact.</summary>
/// <remarks>
/// Version and commit are read from immutable assembly metadata. The remaining properties describe
/// the build context embedded in the application image by CI or a local container build.
/// </remarks>
public sealed record ApplicationMetadata
{
    private const string BuildRunIdMetadataKey = "BuildRunId";
    private const string BuildRunNumberMetadataKey = "BuildRunNumber";
    private const string BuildWorkflowMetadataKey = "BuildWorkflow";
    private const string GitBranchMetadataKey = "GitBranch";
    private const string GitCommitMetadataKey = "GitCommit";
    private const string GitRepositoryMetadataKey = "GitRepository";

    /// <summary>Initializes metadata from the process entry assembly.</summary>
    public ApplicationMetadata()
        : this(Assembly.GetEntryAssembly() ?? typeof(ApplicationMetadata).Assembly)
    {
    }

    /// <summary>Initializes metadata from <paramref name="assembly" />.</summary>
    /// <param name="assembly">Application assembly carrying generated version and source-revision attributes.</param>
    public ApplicationMetadata(Assembly assembly)
    {
        Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "n/a";
        var attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        Repository = GetValue(attributes, GitRepositoryMetadataKey);
        Branch = GetValue(attributes, GitBranchMetadataKey);
        Commit = GetValue(attributes, GitCommitMetadataKey);
        BuildWorkflow = GetValue(attributes, BuildWorkflowMetadataKey);
        BuildRunId = GetValue(attributes, BuildRunIdMetadataKey);
        BuildRunNumber = GetValue(attributes, BuildRunNumberMetadataKey);
    }

    /// <summary>Application semantic version baked into the assembly.</summary>
    public string Version { get; init; }

    /// <summary>Source commit baked into the assembly.</summary>
    public string Commit { get; init; }

    /// <summary>Source repository name.</summary>
    public string Repository { get; init; }

    /// <summary>Git branch or ref used to build the artifact.</summary>
    public string Branch { get; init; }

    /// <summary>GitHub Actions workflow name.</summary>
    public string BuildWorkflow { get; init; }

    /// <summary>GitHub Actions run ID.</summary>
    public string BuildRunId { get; init; }

    /// <summary>GitHub Actions run number.</summary>
    public string BuildRunNumber { get; init; }

    private static string GetValue(IReadOnlyList<AssemblyMetadataAttribute> attributes, string key)
        => attributes.FirstOrDefault(attribute => attribute.Key == key)?.Value ?? "n/a";
}
