namespace CasCap.Common.Configuration.Redis.Models;

/// <summary>Immutable bootstrap settings for the optional, bounded cache-aside trial.</summary>
public sealed record RuntimeConfigurationCacheOptions
{
    /// <summary>Gets a non-secret identifier unique to the durable database/schema or file.</summary>
    public required string AuthorityId { get; init; }

    /// <summary>Gets the maximum cache age; cache fills use up to ten percent shorter lifetimes.</summary>
    public TimeSpan MaximumAge { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the interval for each reader to bypass all caches and check the authority.</summary>
    public TimeSpan ReconciliationInterval { get; init; } = TimeSpan.FromMinutes(1);

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(AuthorityId);
        if (AuthorityId.Length > 200 || MaximumAge < TimeSpan.FromMilliseconds(100)
            || MaximumAge > TimeSpan.FromMinutes(5) || ReconciliationInterval < MaximumAge
            || ReconciliationInterval > TimeSpan.FromMinutes(10))
            throw new ArgumentException("Cache age and reconciliation interval are outside supported bounds.");
    }
}
