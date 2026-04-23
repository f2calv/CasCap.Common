using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>Volatile in-memory session store for interactive console sessions.</summary>
/// <remarks>
/// Sessions are lost when the process exits. Used by
/// <see cref="CasCap.App.Console.ConsoleApp"/> via <see cref="CasCap.Abstractions.ISessionStore"/>.
/// </remarks>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_store.TryGetValue(key, out var json) ? json : null);

    /// <inheritdoc/>
    public Task SetAsync(string key, string json, TimeSpan? slidingExpiration = null)
    {
        _store[key] = json;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix) =>
        Task.FromResult<IReadOnlyList<string>>(_store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList());
}
