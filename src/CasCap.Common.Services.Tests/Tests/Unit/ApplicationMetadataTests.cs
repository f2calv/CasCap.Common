using System.Reflection;
using System.Reflection.Emit;

namespace CasCap.Common.Tests;

/// <summary>Tests for <see cref="ApplicationMetadata" />.</summary>
[Trait("Category", "Metadata")]
public sealed class ApplicationMetadataTests
{
    /// <summary>All artifact and build context values are read from assembly attributes.</summary>
    [Fact]
    public void Constructor_ReadsAssemblyMetadata()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ApplicationMetadataTests-{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        SetAttribute(assembly, typeof(AssemblyInformationalVersionAttribute), "4.2.1-preview.7");
        SetMetadata(assembly, "GitRepository", "example/repository");
        SetMetadata(assembly, "GitBranch", "refs/heads/example");
        SetMetadata(assembly, "GitCommit", "0123456789abcdef");
        SetMetadata(assembly, "BuildWorkflow", "CI");
        SetMetadata(assembly, "BuildRunId", "1234");
        SetMetadata(assembly, "BuildRunNumber", "56");

        var metadata = new ApplicationMetadata(assembly);

        Assert.Equal("4.2.1-preview.7", metadata.Version);
        Assert.Equal("example/repository", metadata.Repository);
        Assert.Equal("refs/heads/example", metadata.Branch);
        Assert.Equal("0123456789abcdef", metadata.Commit);
        Assert.Equal("CI", metadata.BuildWorkflow);
        Assert.Equal("1234", metadata.BuildRunId);
        Assert.Equal("56", metadata.BuildRunNumber);
    }

    private static void SetMetadata(AssemblyBuilder assembly, string key, string value)
    {
        var constructor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [key, value]));
    }

    private static void SetAttribute(AssemblyBuilder assembly, Type attributeType, string value)
    {
        var constructor = attributeType.GetConstructor([typeof(string)])!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [value]));
    }
}