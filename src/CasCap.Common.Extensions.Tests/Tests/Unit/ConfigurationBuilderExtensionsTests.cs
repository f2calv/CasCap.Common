using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

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

    [Fact]
    public void AddGlobalToolConfiguration_PreservesOptionalSnapshotSourcesAndPrecedence()
    {
        var builder = new ConfigurationBuilder();

        builder.AddGlobalToolConfiguration(
            typeof(ConfigurationBuilderExtensionsTests).Assembly,
            AppContext.BaseDirectory,
            AppContext.BaseDirectory);

        var jsonSources = builder.Sources.OfType<JsonConfigurationSource>().ToArray();
        Assert.Equal(4, jsonSources.Length);
        Assert.All(jsonSources, source =>
        {
            Assert.True(source.Optional);
            Assert.False(source.ReloadOnChange);
        });
        Assert.Same(jsonSources[^1], builder.Sources[^2]);
        Assert.EndsWith("secrets.json", jsonSources[^1].Path, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<EnvironmentVariablesConfigurationSource>(builder.Sources[^1]);
    }

    [Theory]
    [InlineData(false, false, false, false, null)]
    [InlineData(true, false, false, false, "installed")]
    [InlineData(true, true, false, false, "project")]
    [InlineData(true, true, false, true, "secret")]
    [InlineData(false, true, false, false, "project")]
    [InlineData(false, false, true, false, "local")]
    [InlineData(true, true, true, false, "local")]
    [InlineData(true, true, true, true, "secret")]
    public void AddGlobalToolConfiguration_BindsOptionalFilesInOrder(
        bool installedSettings,
        bool projectSettings,
        bool localSettings,
        bool userSecrets,
        string? expected)
    {
        var root = Path.Combine(Path.GetTempPath(), $"{nameof(ConfigurationBuilderExtensionsTests)}-{Guid.NewGuid():N}");
        var installedDirectory = Path.Combine(root, "installed");
        var projectDirectory = Path.Combine(root, "project");
        var secretsDirectory = Path.Combine(root, "secrets");
        Directory.CreateDirectory(installedDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(secretsDirectory);
        try
        {
            if (installedSettings)
                File.WriteAllText(Path.Combine(installedDirectory, "appsettings.json"), """{"Value":"installed"}""");
            if (projectSettings)
                File.WriteAllText(Path.Combine(projectDirectory, "appsettings.json"), """{"Value":"project"}""");
            if (localSettings)
                File.WriteAllText(Path.Combine(projectDirectory, "appsettings.Local.json"), """{"Value":"local"}""");
            if (userSecrets)
                File.WriteAllText(Path.Combine(secretsDirectory, "secrets.json"), """{"Value":"secret"}""");

            var builder = new ConfigurationBuilder().AddGlobalToolConfiguration(
                typeof(ConfigurationBuilderExtensionsTests).Assembly,
                installedDirectory,
                projectDirectory);
            using var secretFileProvider = new PhysicalFileProvider(secretsDirectory);
            var secretSource = builder.Sources.OfType<JsonConfigurationSource>().Last();
            secretSource.FileProvider = secretFileProvider;
            secretSource.Path = "secrets.json";
            builder.Sources.OfType<EnvironmentVariablesConfigurationSource>().Single().Prefix = $"{Guid.NewGuid():N}_";
            var configuration = builder.Build();
            using var lifetime = (IDisposable)configuration;

            Assert.Equal(expected, configuration["Value"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, false, "test-local")]
    [InlineData(true, true, "secret")]
    public void AddLocalConfiguration_BindsOptionalProbeFilesBelowUserSecrets(
        bool localSettings,
        bool userSecrets,
        string? expected)
    {
        var root = Directory.CreateTempSubdirectory(nameof(ConfigurationBuilderExtensionsTests));
        try
        {
            if (localSettings)
            {
                File.WriteAllText(Path.Combine(root.FullName, "appsettings.Local.json"), """{"Value":"local"}""");
                File.WriteAllText(Path.Combine(root.FullName, "appsettings.Local.Tests.json"), """{"Value":"local-environment"}""");
                File.WriteAllText(Path.Combine(root.FullName, "appsettings.Tests.Local.json"), """{"Value":"test-local"}""");
            }
            if (userSecrets)
                File.WriteAllText(Path.Combine(root.FullName, "secrets.json"), """{"Value":"secret"}""");

            using var fileProvider = new PhysicalFileProvider(root.FullName);
            var builder = new ConfigurationBuilder()
                .AddLocalConfiguration("Tests")
                .SetFileProvider(fileProvider)
                .AddUserSecrets(typeof(ConfigurationBuilderExtensionsTests).Assembly, optional: true);
            var secretSource = builder.Sources.OfType<JsonConfigurationSource>().Last();
            secretSource.FileProvider = fileProvider;
            secretSource.Path = "secrets.json";
            var configuration = builder.Build();
            using var lifetime = (IDisposable)configuration;

            Assert.Equal(expected, configuration["Value"]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
