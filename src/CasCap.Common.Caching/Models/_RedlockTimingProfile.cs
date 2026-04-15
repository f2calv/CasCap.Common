namespace CasCap.Common.Models;

/// <summary>Timing parameters for a single Redis distributed lock profile.</summary>
/// <remarks>
/// Used as values in <see cref="RedlockConfig.Profiles"/> to define named timing presets.
/// Properties default to cache-miss protection values so partial configuration is safe.
/// </remarks>
public record RedlockTimingProfile
{
    /// <summary>Lock expiry time in milliseconds.</summary>
    /// <remarks>Defaults to <c>5000</c> ms.</remarks>
    public int ExpiryMs { get; init; } = 5_000;

    /// <summary>Maximum wait time to acquire the lock in milliseconds.</summary>
    /// <remarks>Defaults to <c>5000</c> ms.</remarks>
    public int WaitMs { get; init; } = 5_000;

    /// <summary>Retry interval in milliseconds when the lock is not available.</summary>
    /// <remarks>Defaults to <c>250</c> ms.</remarks>
    public int RetryMs { get; init; } = 250;
}
