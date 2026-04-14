namespace CasCap.Common.Models;

/// <summary>Timing parameters for Redis distributed locks (Redlock algorithm).</summary>
public record RedlockConfig
{
    /// <summary>Lock expiry time in milliseconds.</summary>
    /// <remarks>Defaults to <c>15000</c> ms.</remarks>
    public int ExpiryMs { get; init; } = 15_000;

    /// <summary>Maximum wait time to acquire the lock in milliseconds.</summary>
    /// <remarks>Defaults to <c>30000</c> ms.</remarks>
    public int WaitMs { get; init; } = 30_000;

    /// <summary>Retry interval in milliseconds when the lock is not available.</summary>
    /// <remarks>Defaults to <c>10000</c> ms.</remarks>
    public int RetryMs { get; init; } = 10_000;
}
