namespace CasCap.Common.Models;

/// <summary>Well-known profile names for <see cref="RedlockConfig.Profiles"/>.</summary>
public static class RedlockProfiles
{
    /// <summary>Profile tuned for short-lived cache-miss protection locks.</summary>
    public const string CacheMiss = nameof(CacheMiss);

    /// <summary>Profile tuned for long-lived leadership-election locks.</summary>
    public const string LeaderElection = nameof(LeaderElection);
}
