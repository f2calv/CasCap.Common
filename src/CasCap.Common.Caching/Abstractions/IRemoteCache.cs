namespace CasCap.Abstractions;

/// <summary>
/// The <see cref="IRemoteCache"/> interface abstracts core functionality that
/// should be present in an underlying cache service.
/// </summary>
/// <remarks>
/// Note: currently the only supported external cache is Redis, so this abstraction
/// is actually not really abstracted properly, when we plug in another external
/// cache provider this will change!
/// </remarks>
public interface IRemoteCache
{
    /// <summary>
    /// Exposes the <see cref="IConnectionMultiplexer"/> for the currently active Redis connection.
    /// </summary>
    IConnectionMultiplexer Connection { get; }

    /// <summary>
    /// Exposes the Redis <see cref="IDatabase"/> from the currently active Redis connection.
    /// </summary>
    IDatabase Db { get; }

    /// <summary>
    /// Exposes the Redis <see cref="ISubscriber"/> to use pub/sub for the currently active Redis connection.
    /// </summary>
    ISubscriber Subscriber { get; }

    /// <summary>
    /// Obtain configuration API for the first configured server via <see cref="IServer"/> to allow direct configuration of the Redis instance.
    /// </summary>
    IServer Server { get; }

    /// <summary>
    /// Get object from cache casting it to a <see cref="string"/> upon retrieval.
    /// </summary>
    string? Get(string key, CommandFlags flags = CommandFlags.None);

    /// <summary>
    /// Get object from cache casting it to a <see cref="byte"/> array upon retrieval.
    /// </summary>
    byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="Get(string, CommandFlags)"/>
    Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="GetBytes(string, CommandFlags)"/>
    Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None);

    /// <summary>
    /// Add a <see cref="byte"/> array to the cache.
    /// </summary>
    bool Set(string key, byte[] value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None);

    /// <summary>
    /// Add a <see cref="string"/> object to the cache.
    /// </summary>
    bool Set(string key, string value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="Set(string, byte[], TimeSpan?, DateTimeOffset?, CommandFlags)"/>
    Task<bool> SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="Set(string, string, TimeSpan?, DateTimeOffset?, CommandFlags)"/>
    Task<bool> SetAsync(string key, string value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None);

    /// <summary>
    /// Delete an object from the cache.
    /// </summary>
    bool Delete(string key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="Delete(string, CommandFlags)"/>
    Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None);

    /// <summary>
    /// Collection keeps track of the cache item requested sliding expirations.
    /// When we attempt to a previously cached item we also send in the sliding
    /// expiration again to push the Redis expiration forward.
    /// </summary>
    ConcurrentDictionary<string, TimeSpan> SlidingExpirations { get; set; }

    /// <summary>
    /// Leverages <see cref="IDatabaseAsync.StringGetWithExpiryAsync(RedisKey, CommandFlags)"/> to return the object
    /// plus meta data i.e. object expiry information.
    /// </summary>
    /// <remarks>
    /// If a Sliding expiration has been used for this key, then this method uses a custom LUA script
    /// which allows for re-adjusting the sliding expiration value to what it was initially set.
    /// </remarks>
    Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithExpiryAsync<T>
        (string key, CommandFlags flags = CommandFlags.None, bool updateSlidingExpirationIfExists = true, [CallerMemberName] string caller = "");

    /// <summary>
    /// Exposes a dictionary of LuaScripts to allow management of scripts during connection.
    /// </summary>
    Dictionary<string, LoadedLuaScript> LuaScripts { get; set; }

    /// <summary>
    /// Loads a custom script into the <see cref="LuaScripts"/> collection.
    /// </summary>
    /// <param name="scriptName">Should be of the form "Namespace.Class.ScriptName.lua", e.g. CasCap.Resources.MyScript.lua</param>
    LoadedLuaScript? LoadLuaScript(Assembly assembly, string scriptName);
}
