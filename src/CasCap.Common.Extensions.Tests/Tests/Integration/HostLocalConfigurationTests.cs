using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Hosting;

namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests local overlays against the Generic Host's actual default configuration sources.</summary>
[Trait("Category", "Integration")]
public sealed class HostLocalConfigurationTests
{
    [Theory]
    [InlineData(false, false, false, false, "deployment")]
    [InlineData(true, false, false, false, "test-local")]
    [InlineData(true, true, false, false, "secret")]
    [InlineData(true, true, true, false, "environment")]
    [InlineData(true, true, true, true, "command")]
    public void AddLocalConfiguration_PreservesDefaultHostOverridePrecedence(
        bool localSettings,
        bool userSecrets,
        bool environmentVariables,
        bool commandLine,
        string expected)
    {
        var root = Directory.CreateTempSubdirectory(nameof(HostLocalConfigurationTests));
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "appsettings.json"), """{"Value":"base"}""");
            File.WriteAllText(Path.Combine(root.FullName, "appsettings.Development.json"), """{"Value":"deployment"}""");
            if (localSettings)
            {
                File.WriteAllText(Path.Combine(root.FullName, "appsettings.Local.json"), """{"Value":"local"}""");
                File.WriteAllText(Path.Combine(root.FullName, "appsettings.Local.Development.json"), """{"Value":"local-environment"}""");
                File.WriteAllText(Path.Combine(root.FullName, "appsettings.Tests.Local.json"), """{"Value":"test-local"}""");
            }
            if (userSecrets)
                File.WriteAllText(Path.Combine(root.FullName, "secrets.json"), """{"Value":"secret"}""");

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ApplicationName = typeof(object).Assembly.GetName().Name,
                ContentRootPath = root.FullName,
                EnvironmentName = Environments.Development,
                Args = commandLine ? ["Value=command"] : [],
            });
            var environmentSource = builder.Configuration.Sources
                .OfType<EnvironmentVariablesConfigurationSource>()
                .Single(source => string.IsNullOrEmpty(source.Prefix));
            var environmentIndex = builder.Configuration.Sources.IndexOf(environmentSource);
            builder.Configuration.Sources[environmentIndex] = new MemoryConfigurationSource
            {
                InitialData = environmentVariables
                    ? [new KeyValuePair<string, string?>("Value", "environment")]
                    : [],
            };
            if (userSecrets)
                builder.Configuration.Sources.Insert(environmentIndex, new JsonConfigurationSource
                {
                    FileProvider = builder.Environment.ContentRootFileProvider,
                    Path = "secrets.json",
                    Optional = false,
                });

            builder.Configuration.AddLocalConfiguration(builder.Environment.EnvironmentName);
            using var host = builder.Build();

            Assert.Equal(expected, builder.Configuration["Value"]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
