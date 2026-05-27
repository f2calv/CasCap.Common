using System.Collections.Concurrent;

namespace CasCap.Common.Services;

/// <summary>Volatile in-memory session store for interactive console sessions.</summary>
/// <remarks>
/// Sessions are lost when the process exits. Used by
/// <see cref="CasCap.App.Console.ConsoleApp"/> via <see cref="CasCap.Abstractions.ISessionStore"/>.
/// </remarks>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    /// <inheritdoc/>
    public ValueTask<string?> GetAsync(string key) =>
        new(_store.TryGetValue(key, out var json) ? json : null);

    /// <inheritdoc/>
    public ValueTask SetAsync(string key, string json, TimeSpan? slidingExpiration = null)
    {
        _store[key] = json;
        return default;
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string key)
    {
        _store.TryRemove(key, out _);
        return default;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix) =>
        new(_store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList());
}
