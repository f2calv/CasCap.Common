namespace CasCap.Services;

public class DiskCacheService : ILocalCache
{
    readonly ILogger _logger;
    readonly CachingOptions _cachingOptions;

    public string DiskCacheFolder { get; set; } = string.Empty;

    public DiskCacheService(ILogger<DiskCacheService> logger, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
        DiskCacheFolder = _cachingOptions.DiskCacheFolder;
        if (!Directory.Exists(DiskCacheFolder)) Directory.CreateDirectory(DiskCacheFolder);
        if (_cachingOptions.DiskCache.ClearOnStartup) DeleteAll();
    }

    public long DeleteAll()
    {
        var di = new DirectoryInfo(DiskCacheFolder);
        var files = 0L;
        foreach (var file in di.GetFiles())
        {
            _logger.LogTrace("{className} attempting deletion of file {fileName}", nameof(DiskCacheService), file.Name);
            file.Delete();
            files++;
        }
        var directories = 0L;
        foreach (var dir in di.GetDirectories())
        {
            _logger.LogTrace("{className} attempting deletion of directory {directoryName}", nameof(DiskCacheService), dir.Name);
            dir.Delete(true);
            directories++;
        }
        return files;
    }

    public T? Get<T>(string key)
    {
        key = GetKey(key);
        T? cacheEntry;
        if (File.Exists(key))
        {
            _logger.LogTrace("{className} retrieved object with {key} from local cache", nameof(DiskCacheService), key);
            if (_cachingOptions.DiskCache.SerializationType == SerializationType.Json)
            {
                var json = File.ReadAllText(key);
                cacheEntry = json.FromJson<T>();
            }
            else if (_cachingOptions.DiskCache.SerializationType == SerializationType.MessagePack)
            {
                var bytes = File.ReadAllBytes(key);
                cacheEntry = bytes.FromMessagePack<T>();
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.DiskCache.SerializationType)} {_cachingOptions.DiskCache.SerializationType} is not supported!");
        }
        else
        {
            _logger.LogTrace("{className} could not retrieve object with {key} from local cache", nameof(DiskCacheService), key);
            cacheEntry = default;
        }
        return cacheEntry;
    }

    public void Set<T>(string key, T cacheEntry, TimeSpan? expiry = null)
    {
        key = GetKey(key);
        //TODO: plug in expiry service via DiskCacheInvalidationBgService ?
        _logger.LogTrace("{className} attempting to store object with {key} in local cache", nameof(DiskCacheService), key);
        if (cacheEntry != null)
        {
            if (_cachingOptions.DiskCache.SerializationType == SerializationType.Json)
            {
                var json = cacheEntry.ToJson();
                File.WriteAllText(key, json);
            }
            else if (_cachingOptions.DiskCache.SerializationType == SerializationType.MessagePack)
            {
                var bytes = cacheEntry.ToMessagePack();
                File.WriteAllBytes(key, bytes);
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.DiskCache.SerializationType)} {_cachingOptions.DiskCache.SerializationType} is not supported!");
        }
    }

    string GetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(DiskCacheFolder))
            throw new ArgumentException($"to use {nameof(DiskCacheService)} you must set the {nameof(_cachingOptions.DiskCacheFolder)}");
        return Path.Combine(DiskCacheFolder, key.Replace(":", "_"));
    }

    [ExcludeFromCodeCoverage(Justification = "not yet plugged into the interface")]
    public async Task<T?> GetAsync<T>(string key, Func<Task<T>>? createItem = null, CancellationToken token = default) where T : class
    {
        key = GetKey(key);
        T? cacheEntry = default;
        if (File.Exists(key))
        {
            var json = await File.ReadAllTextAsync(key, token);
            try
            {
                cacheEntry = json.FromJson<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{className} deserialization error for {key}", nameof(DiskCacheService), key);
            }
            _logger.LogTrace("{className} retrieved cacheEntry {key}", nameof(DiskCacheService), key);
        }
        else if (createItem is not null)
        {
            //we lock here to prevent multiple creations at the same time
            using (await AsyncDuplicateLock.LockAsync(key).ConfigureAwait(false))
            {
                // Key not in cache, so populate
                cacheEntry = await createItem();
                _logger.LogTrace("{className} attempted to populate a new cacheEntry object {key}", nameof(DiskCacheService), key);
                if (cacheEntry != null)
                    Set(key, cacheEntry, null);
            }
        }
        return cacheEntry;
    }

    public bool Delete(string key)
    {
        key = GetKey(key);
        if (File.Exists(key))
        {
            File.Delete(key);
            return true;
        }
        return false;
    }
}
