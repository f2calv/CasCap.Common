namespace CasCap.Common.Models;

/// <summary>Configures process-wide host shutdown behavior.</summary>
public sealed record HostLifecycleConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName =>
        $"{nameof(CasCap)}:{nameof(HostLifecycleConfig)}";

    /// <summary>
    /// Graceful shutdown timeout in milliseconds the host waits for in-flight work to drain before
    /// forcing termination.
    /// </summary>
    /// <remarks>Defaults to <c>30000</c> milliseconds.</remarks>
    [Range(1_000, 600_000)]
    public int ShutdownTimeoutMs { get; init; } = 30_000;
}
