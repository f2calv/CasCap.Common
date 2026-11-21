namespace CasCap.Common.Caching.Tests;

internal sealed class CacheTestConnectionStringsConfig
{
    public const string ConfigurationSectionName = "ConnectionStrings";

    public string? Redis { get; init; }
}
