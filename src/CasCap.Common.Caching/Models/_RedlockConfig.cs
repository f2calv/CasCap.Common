namespace CasCap.Common.Models;

/// <summary>Timing parameters for Redis distributed locks (Redlock algorithm) with named profile support.</summary>
/// <remarks>
/// Root-level properties serve as the default timing (tuned for cache-miss protection).
/// Named profiles in <see cref="Profiles"/> provide alternative timings for other use cases
/// (e.g. leadership election). Use <see cref="GetTimings"/> to resolve a profile by name.
/// </remarks>
public record RedlockConfig
{
    /// <summary>Well-known profile name for cache-miss protection locks.</summary>
    public const string CacheMiss = nameof(CacheMiss);

    /// <summary>Well-known profile name for leadership-election locks.</summary>
    public const string LeaderElection = nameof(LeaderElection);

    /// <summary>Lock expiry time in milliseconds.</summary>
    /// <remarks>Defaults to <c>5000</c> ms. Used by <see cref="CasCap.Common.Services.DistributedCacheService"/>.</remarks>
    public int ExpiryMs { get; init; } = 5_000;

    /// <summary>Maximum wait time to acquire the lock in milliseconds.</summary>
    /// <remarks>Defaults to <c>5000</c> ms. Used by <see cref="CasCap.Common.Services.DistributedCacheService"/>.</remarks>
    public int WaitMs { get; init; } = 5_000;

    /// <summary>Retry interval in milliseconds when the lock is not available.</summary>
    /// <remarks>Defaults to <c>250</c> ms. Used by <see cref="CasCap.Common.Services.DistributedCacheService"/>.</remarks>
    public int RetryMs { get; init; } = 250;

    /// <summary>Named timing profiles for different distributed lock use cases.</summary>
    /// <remarks>
    /// Includes a built-in <see cref="LeaderElection"/> profile by default.
    /// Custom profiles can be added via <c>appsettings.json</c>.
    /// </remarks>
    public Dictionary<string, RedlockTimingProfile> Profiles { get; init; } = new()
    {
        [LeaderElection] = new() { ExpiryMs = 30_000, WaitMs = 60_000, RetryMs = 5_000 }
    };

    /// <summary>
    /// Resolves timing values for the specified profile name.
    /// Falls back to the root-level defaults when the profile is <c>null</c> or not found.
    /// </summary>
    /// <param name="profileName">Profile key from <see cref="Profiles"/>, or <c>null</c> for root defaults.</param>
    public (TimeSpan expiry, TimeSpan wait, TimeSpan retry) GetTimings(string? profileName = null)
    {
        if (profileName is not null && Profiles.TryGetValue(profileName, out var p))
            return (TimeSpan.FromMilliseconds(p.ExpiryMs), TimeSpan.FromMilliseconds(p.WaitMs), TimeSpan.FromMilliseconds(p.RetryMs));
        return (TimeSpan.FromMilliseconds(ExpiryMs), TimeSpan.FromMilliseconds(WaitMs), TimeSpan.FromMilliseconds(RetryMs));
    }
}
