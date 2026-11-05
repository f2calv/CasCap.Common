namespace CasCap.Common.Models;

/// <summary>Controls periodic application metadata diagnostics.</summary>
public sealed record ApplicationMetadataConfig
{
    /// <summary>Configuration section name.</summary>
    public static string ConfigurationSectionName => nameof(ApplicationMetadataConfig);

    /// <summary>Interval between application metadata log entries.</summary>
    /// <remarks>
    /// Defaults to fifteen minutes, so an idle host writes a periodic heartbeat rather than a line every minute
    /// at <c>Information</c>.
    /// </remarks>
    public TimeSpan LogInterval { get; init; } = TimeSpan.FromMinutes(15);
}
