namespace CasCap.Common.Services;

/// <summary>No-op <see cref="IRemoteCache"/> used when no Redis connection is configured.</summary>
public sealed class NullRemoteCache : IRemoteCache
{
    /// <summary>Initializes a new instance of the <see cref="NullRemoteCache"/> class.</summary>
    /// <exception cref="InvalidOperationException">Thrown when remote caching is enabled.</exception>
    public NullRemoteCache(IOptions<CachingConfig> cachingConfig)
    {
        if (cachingConfig.Value.RemoteCache.IsEnabled)
            throw new InvalidOperationException(
                $"{nameof(NullRemoteCache)} cannot be used when {nameof(CachingConfig.RemoteCache)}.{nameof(CacheParameters.IsEnabled)} is true. Configure a Redis connection.");
    }

    /// <inheritdoc/>
    public IConnectionMultiplexer Connection
        => throw new NotSupportedException($"{nameof(NullRemoteCache)} does not provide a Redis {nameof(IConnectionMultiplexer)}.");

    /// <inheritdoc/>
    public IDatabase Db
        => throw new NotSupportedException($"{nameof(NullRemoteCache)} does not provide a Redis {nameof(IDatabase)}.");

    /// <inheritdoc/>
    public ISubscriber Subscriber
        => throw new NotSupportedException($"{nameof(NullRemoteCache)} does not provide a Redis {nameof(ISubscriber)}.");

    /// <inheritdoc/>
    public IServer Server
        => throw new NotSupportedException($"{nameof(NullRemoteCache)} does not provide a Redis {nameof(IServer)}.");

    /// <inheritdoc/>
    public ConcurrentDictionary<string, TimeSpan> SlidingExpirations { get; } = [];

    /// <inheritdoc/>
    public Dictionary<string, LoadedLuaScript> LuaScripts { get; set; } = [];

    /// <inheritdoc/>
    public string? Get(string key, CommandFlags flags = CommandFlags.None) => null;

    /// <inheritdoc/>
    public byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None) => null;

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None)
        => Task.FromResult<string?>(null);

    /// <inheritdoc/>
    public Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None)
        => Task.FromResult<byte[]?>(null);

    /// <inheritdoc/>
    public bool Set(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => false;

    /// <inheritdoc/>
    public bool Set(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => false;

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => Task.FromResult(false);

    /// <inheritdoc/>
    public ValueTask<bool> ExtendSlidingExpirationAsync(string key, CommandFlags flags = CommandFlags.FireAndForget)
        => default;

    /// <inheritdoc/>
    public bool Delete(string key, CommandFlags flags = CommandFlags.None) => false;

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithExpiryAsync<T>(
        string key, CommandFlags flags = CommandFlags.None, bool updateSlidingExpirationIfExists = true,
        [CallerMemberName] string caller = "")
        => Task.FromResult<(TimeSpan? expiry, T? cacheEntry)>((null, default));

    /// <inheritdoc/>
    public LoadedLuaScript? LoadLuaScript(string scriptName, string script) => null;
}