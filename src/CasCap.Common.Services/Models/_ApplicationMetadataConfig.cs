namespace CasCap.Common.Models;

/// <summary>Controls periodic application metadata diagnostics.</summary>
public sealed record ApplicationMetadataConfig
{
    /// <summary>Configuration section name.</summary>
    public static string ConfigurationSectionName => nameof(ApplicationMetadataConfig);

    /// <summary>Interval between application metadata log entries.</summary>
    /// <remarks>Defaults to one minute.</remarks>
    public TimeSpan LogInterval { get; init; } = TimeSpan.FromMinutes(1);
}