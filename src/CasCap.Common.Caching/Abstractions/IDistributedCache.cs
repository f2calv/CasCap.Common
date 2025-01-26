namespace CasCap.Abstractions;

/// <summary>
/// The <see cref="IDistributedCache"/> is a wrapper around the <see cref="ILocalCache"/>
/// and <see cref="IRemoteCache"/> to facilitate both local and remote caching of key objects.
/// </summary>
public interface IDistributedCache
{
    /// <summary>
    /// Event fires when an object is evicted from the cache.
    /// </summary>
    /// <remarks>
    /// Evictions happen generally due to either a hard limit being reached of number of items stored in the cache or due to a memory pressure event.
    /// </remarks>
    event EventHandler<PostEvictionEventArgs> PostEvictionEvent;

    /// <summary>
    /// Retrieve an object from the cache, first by checking the <see cref="ILocalCache"/> and if the
    /// object is not found we then check the <see cref="IRemoteCache"/>. If the object is found only in the <see cref="IRemoteCache"/>
    /// we'll also then cache it in the <see cref="ILocalCache"/> before returing the result to the caller.
    /// </summary>
    Task<T?> Get<T>(string key) where T : class;

    /// <inheritdoc cref="IDistributedCache.Get{T}(string)"/>
    Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None) where T : class;

    /// <summary>
    /// Add an object to both the <see cref="ILocalCache"/> and <see cref="IRemoteCache"/> caches.
    /// </summary>
    Task Set<T>(string key, T cacheEntry) where T : class;

    /// <inheritdoc cref="Set{T}(string, T)"/>
    Task Set<T>(string key, T cacheEntry, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null,
        CommandFlags flags = CommandFlags.None) where T : class;

    /// <summary>
    /// Delete an object from both <see cref="ILocalCache"/> and <see cref="IRemoteCache"/>.
    /// </summary>
    Task<bool> Delete(string key, CommandFlags flags = CommandFlags.None);

    /// <summary>
    /// Delete all objects from both <see cref="ILocalCache"/> and <see cref="IRemoteCache"/>.
    /// </summary>
    Task<long> DeleteAll(CommandFlags flags = CommandFlags.None, CancellationToken cancellationToken = default);
}
