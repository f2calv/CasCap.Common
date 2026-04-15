namespace CasCap.Common.Xunit;

/// <summary>Skips the test when running in an Azure DevOps build.</summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipIfAzureDevOpsBuildFactAttribute() : FactAttribute
{
    /// <inheritdoc/>
    public override string? Skip
    {
        get => IsAzureDevOps() ? "Ignore test when running an Azure DevOps build" : base.Skip;
        set => base.Skip = value;
    }

    private static bool IsAzureDevOps() => Environment.GetEnvironmentVariable("TF_BUILD") is not null;
}
