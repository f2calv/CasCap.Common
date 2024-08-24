namespace CasCap.Abstractions;

public interface ILocalCacheService
{
    void Set<T>(string key, T cacheEntry, TimeSpan? expiry = null);
    T? Get<T>(string key);
    bool Delete(string key);
    long DeleteAll();
}
