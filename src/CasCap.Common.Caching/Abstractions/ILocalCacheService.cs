namespace CasCap.Abstractions;

public interface ILocalCacheService
{
    void SetLocal<T>(string key, T cacheEntry, TimeSpan? expiry);
    T? Get<T>(string key);
    void DeleteLocal(string key, bool viaPubSub);
}
