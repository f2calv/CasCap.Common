namespace CasCap.Services;

/// <summary>Redis-backed session store for persistent agent sessions.</summary>
/// <remarks>
/// <para>
/// Wraps <see cref="IDistributedCache"/> (from <c>CasCap.Common</c>) to implement
/// <see cref="ISessionStore"/>. Used by <c>CommunicationsBgService</c>.
/// </para>
/// <para>
/// This store already provides Redis-backed persistence for serialised
/// <see cref="Microsoft.Agents.AI.AgentSession"/> state. It functions as the
/// project's Redis chat history provider, persisting the full session JSON
/// (including the <see cref="Microsoft.Agents.AI.InMemoryChatHistoryProvider"/>
/// state) under a key derived from <see cref="AgentConfig.Name"/>
/// (e.g. <c>agents:sessions:{name}:active</c>).
/// </para>
/// <para>
/// When Microsoft ships a first-party .NET <c>RedisChatHistoryProvider</c> for
/// Agent Framework (currently Python-only, see
/// <see href="https://learn.microsoft.com/en-us/agent-framework/integrations/" />),
/// evaluate replacing this manual session serialisation with the built-in provider
/// and simplifying <see cref="AgentCommandHandler.LoadSessionAsync"/> /
/// <see cref="AgentCommandHandler.SaveSessionAsync"/>.
/// </para>
/// </remarks>
public sealed class DistributedCacheSessionStore(IDistributedCache distCache) : ISessionStore
{
    /// <inheritdoc/>
    public async Task<string?> GetAsync(string key) =>
        await distCache.Get<string>(key);

    /// <inheritdoc/>
    public async Task SetAsync(string key, string json, TimeSpan? slidingExpiration = null) =>
        await distCache.Set(key, json, slidingExpiration: slidingExpiration);

    /// <inheritdoc/>
    public async Task DeleteAsync(string key) =>
        await distCache.Delete(key);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
