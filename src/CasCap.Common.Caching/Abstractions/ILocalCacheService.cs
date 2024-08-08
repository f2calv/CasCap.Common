namespace CasCap.Abstractions;

public interface ILocalCacheService
{
    void SetLocal<T>(string key, T cacheEntry, TimeSpan? expiry = null);
    T? Get<T>(string key);
    bool DeleteLocal(string key);
}
