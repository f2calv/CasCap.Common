using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for standard configuration provider ordering.</summary>
public sealed class ConfigurationBuilderExtensionsTests
{
    /// <summary>Verifies environment variables override user secrets.</summary>
    [Fact]
    public void AddStandardConfiguration_EnvironmentVariablesFollowUserSecrets()
    {
        var builder = new ConfigurationBuilder();

        builder.AddStandardConfiguration("Test", typeof(ConfigurationBuilderExtensionsTests).Assembly);

        var environmentVariablesIndex = builder.Sources.Count - 1;
        Assert.IsType<EnvironmentVariablesConfigurationSource>(builder.Sources[environmentVariablesIndex]);
        var userSecretsSource = Assert.IsType<JsonConfigurationSource>(
            builder.Sources[environmentVariablesIndex - 1]);
        Assert.EndsWith("secrets.json", userSecretsSource.Path, StringComparison.OrdinalIgnoreCase);
    }
}