namespace CasCap.Abstractions;

public interface ILocalCache
{
    /// <summary>
    /// Adds an object to the cache, with optional expiration.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="cacheEntry"></param>
    /// <param name="relativeExpirationRelativeToNow"></param>
    /// <param name="absoluteExpirationRelativeToNow"></param>
    void Set<T>(string key, T cacheEntry, TimeSpan? relativeExpiration = null, TimeSpan? absoluteExpiration = null);

    /// <summary>
    /// Retrieves an object from the cache matching the given <see cref="key"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    T? Get<T>(string key);

    /// <summary>
    /// Deletes a single object from the cache matching the given <see cref="key"/>.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    bool Delete(string key);

    /// <summary>
    /// Deletes all objects from the cache.
    /// </summary>
    /// <returns>The number of cached objects removed.</returns>
    long DeleteAll();
}
