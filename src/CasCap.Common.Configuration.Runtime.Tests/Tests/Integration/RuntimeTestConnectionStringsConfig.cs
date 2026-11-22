using CasCap.Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

internal sealed class RuntimeTestConnectionStringsConfig
{
    public const string ConfigurationSectionName = "ConnectionStrings";

    public string? Postgres { get; init; }

    public string? Redis { get; init; }

    internal static RuntimeTestConnectionStringsConfig Load()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddStandardConfiguration("Tests")
            .AddEnvironmentVariables("RuntimeConfigurationTests__");
        return configuration.GetSection(ConfigurationSectionName).Get<RuntimeTestConnectionStringsConfig>() ?? new();
    }
}
