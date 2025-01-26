namespace CasCap.Abstractions;

public interface ILocalCache
{
    /// <summary>
    /// Adds an object to the cache, with optional expiration parameters.
    /// </summary>
    void Set<T>(string key, T cacheEntry, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null);

    /// <summary>
    /// Retrieves an object from the cache matching the given <paramref name="key"/>.
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// Deletes a single object from the cache matching the given <paramref name="key"/>.
    /// </summary>
    bool Delete(string key);

    /// <summary>
    /// Deletes all objects from the cache.
    /// </summary>
    /// <returns>The number of cached objects removed.</returns>
    long DeleteAll();
}
