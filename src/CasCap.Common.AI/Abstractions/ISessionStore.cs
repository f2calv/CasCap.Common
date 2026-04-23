namespace CasCap.Abstractions;

/// <summary>Persistence abstraction for serialised agent session state.</summary>
/// <remarks>
/// Implementations include <see cref="CasCap.Services.InMemorySessionStore"/> (volatile, console)
/// and <see cref="CasCap.Services.DistributedCacheSessionStore"/> (Redis-backed, background service).
/// The key is derived from <see cref="CasCap.Models.AgentConfig.Name"/> by
/// <see cref="CasCap.Services.AgentCommandHandler"/> (e.g. <c>agents:sessions:{name}:active</c>).
/// </remarks>
public interface ISessionStore
{
    /// <summary>Retrieves the serialised session JSON for <paramref name="key"/>, or <see langword="null"/> when absent.</summary>
    Task<string?> GetAsync(string key);

    /// <summary>Persists <paramref name="json"/> under <paramref name="key"/> with an optional sliding expiration.</summary>
    Task SetAsync(string key, string json, TimeSpan? slidingExpiration = null);

    /// <summary>Removes the session stored under <paramref name="key"/>.</summary>
    Task DeleteAsync(string key);

    /// <summary>Lists all keys that match <paramref name="prefix"/>.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix);
}
