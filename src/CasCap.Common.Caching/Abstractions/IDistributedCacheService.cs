﻿namespace CasCap.Abstractions;

public interface IDistributedCacheService
{
    event EventHandler<PostEvictionEventArgs> PostEvictionEvent;

    //todo: create 2x overload options to accept ttl(expiry) of a utc datetime
    Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class;
    //Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class;

    Task Set<T>(string key, T cacheEntry, int ttl = -1) where T : class;
    //Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class;

    Task<bool> Delete(string key);

    Task<long> DeleteAll(CancellationToken cancellationToken);
}