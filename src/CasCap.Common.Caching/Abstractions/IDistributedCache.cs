namespace CasCap.Abstractions;

/// <summary>
/// The <see cref="IDistributedCache"/> is a wrapper around both <see cref="ILocalCache"/> and <see cref="IRemoteCache"/>
/// to facilitate both local and remote caching of key objects.
/// </summary>
public interface IDistributedCache
{
    event EventHandler<PostEvictionEventArgs> PostEvictionEvent;

    //todo: create 2x overload options to accept ttl(expiry) of a utc datetime
    Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1, CommandFlags flags = CommandFlags.None) where T : class;
    //Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class;

    Task Set<T>(string key, T cacheEntry, int ttl = -1, CommandFlags flags = CommandFlags.None) where T : class;
    //Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class;

    Task<bool> Delete(string key, CommandFlags flags = CommandFlags.None);

    /// <summary>
    /// Delete all objects from the cache
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<long> DeleteAll(CancellationToken cancellationToken = default);
}
