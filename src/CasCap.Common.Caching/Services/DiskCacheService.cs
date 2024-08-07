﻿using System.Diagnostics;
using System.IO;
namespace CasCap.Services;

public class DiskCacheService : ILocalCacheService
{
    readonly ILogger _logger;
    readonly CachingOptions _cachingOptions;

    public DiskCacheService(ILogger<DiskCacheService> logger, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
    }

    public string CacheSize()
    {
        var size = Utils.CalculateFolderSize(_cachingOptions.DiskCacheFolder);
        if (size > 1024)
        {
            var s = size / 1024;
            return $"{s:###,###,##0}kb";
        }
        else
            return $"0kb";
    }

    public (int files, int directories) CacheClear()
    {
        var di = new DirectoryInfo(_cachingOptions.DiskCacheFolder);
        var files = 0;
        foreach (var file in di.GetFiles())
        {
            file.Delete();
            files++;
        }
        var directories = 0;
        foreach (var dir in di.GetDirectories())
        {
            dir.Delete(true);
            directories++;
        }
        return (files, directories);
    }

    public T? Get<T>(string key)
    {
        key = GetKey(key);
        T cacheEntry;
        if (File.Exists(key))
        {
            if (_cachingOptions.DiskCacheSerialisationType == SerialisationType.Json)
            {
                var json = File.ReadAllText(key);
                cacheEntry = json.FromJSON<T>();
            }
            else if (_cachingOptions.DiskCacheSerialisationType == SerialisationType.MessagePack)
            {
                var bytes = File.ReadAllBytes(key);
                cacheEntry = bytes.FromMessagePack<T>();
            }
            else
                throw new NotSupportedException();
        }
        else
            cacheEntry = default;
        return cacheEntry;
    }

    public void SetLocal<T>(string key, T cacheEntry, TimeSpan? expiry)
    {
        //TODO: plug in expiry service via DiskCacheInvalidationBgService ?
        _logger.LogTrace("{serviceName} attempted to populate a new cacheEntry object {key}", nameof(DiskCacheService), key);
        if (cacheEntry != null)
        {
            if (_cachingOptions.DiskCacheSerialisationType == SerialisationType.Json)
            {
                var json = cacheEntry.ToJSON();
                File.WriteAllText(key, json);
            }
            else if (_cachingOptions.DiskCacheSerialisationType == SerialisationType.MessagePack)
            {
                var bytes = cacheEntry.ToMessagePack();
                File.WriteAllBytes(key, bytes);
            }
            else
                throw new NotSupportedException();
        }
    }

    string GetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(_cachingOptions.DiskCacheFolder))
            throw new ArgumentException($"to use {nameof(DiskCacheService)} you must set the {nameof(_cachingOptions.DiskCacheFolder)}");
        return Path.Combine(_cachingOptions.DiskCacheFolder, key);
    }

    public async Task<T> GetAsync<T>(string key, Func<Task<T>> createItem = null, CancellationToken token = default) where T : class
    {
        key = GetKey(key);
        T cacheEntry = default;
        if (File.Exists(key))
        {
            var json = File.ReadAllText(key);
            try
            {
                cacheEntry = json.FromJSON<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{serviceName} deserialisation error for {key}", nameof(DiskCacheService), key);
                Debugger.Break();
            }
            _logger.LogTrace("{serviceName} retrieved cacheEntry {key}", nameof(DiskCacheService), key);
        }
        else if (createItem is not null)
        {
            //we lock here to prevent multiple creations at the same time
            using (await AsyncDuplicateLock.LockAsync(key).ConfigureAwait(false))
            {
                // Key not in cache, so populate
                cacheEntry = await createItem();
                _logger.LogTrace("{serviceName} attempted to populate a new cacheEntry object {key}", nameof(DiskCacheService), key);
                if (cacheEntry != null)
                    SetLocal(key, cacheEntry, null);
            }
        }
        return cacheEntry;
    }

    public void DeleteLocal(string key, bool viaPubSub)
    {
        key = GetKey(key);
        if (File.Exists(key))
            File.Delete(key);
    }
}
