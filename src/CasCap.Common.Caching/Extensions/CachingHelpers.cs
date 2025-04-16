namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods to assist with common Caching tasks.
/// </summary>
public static class CachingHelpers
{
    /// <summary>
    /// Calculates the relative expiration <see cref="TimeSpan"/> from an integer.
    /// </summary>
    public static TimeSpan? GetExpirationFromSeconds(this int ttl)
    {
        TimeSpan? expiry = null;
        if (ttl > -1)//if -1, the key does not have expiry timeout.
            expiry = TimeSpan.FromSeconds(ttl);
        return expiry;
    }
}
