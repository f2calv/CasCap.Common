namespace CasCap.Services;

/// <summary>
/// The <see cref="DiskCacheService"/> is an implementation of the <see cref="ILocalCache"/> which
/// uses the physical disk to implement the same functionality as the <see cref="IMemoryCache"/>.
/// </summary>
public class DiskCacheService : ILocalCache
{
    private readonly ILogger _logger;
    private readonly CachingOptions _cachingOptions;
    private string _diskCacheFolder { get; set; } = string.Empty;

    /// <summary>
    /// Collection keeps track of the cache items sliding expirations.
    /// When we retrieve a previously cached item we must re-use the sliding sliding expiration again
    /// to update <see cref="_absoluteExpirations"/> with the absolute expiration.
    /// </summary>
    /// <remarks>
    /// TODO: also cache this collection locally and reload on startup!
    /// </remarks>
    private readonly ConcurrentDictionary<string, TimeSpan> _slidingExpirations = [];

    /// <summary>
    /// Collection keeps track of the cache items absolute expirations.
    /// When we retrieve a previously cached item we use the absolute expiration value to expire the item if expired.
    /// </summary>
    /// <remarks>
    /// TODO: In future we could create some sort of background service to auto-expire the items, e.g. DiskCacheInvalidationBgService ?
    /// TODO: also cache this collection locally and reload on startup!
    /// </remarks>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _absoluteExpirations = [];

    /// <inheritdoc/>
    public DiskCacheService(ILogger<DiskCacheService> logger, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
        _diskCacheFolder = _cachingOptions.DiskCacheFolder;
        if (!Directory.Exists(_diskCacheFolder)) Directory.CreateDirectory(_diskCacheFolder);
        if (_cachingOptions.DiskCache.ClearOnStartup) DeleteAll();
    }

    /// <inheritdoc/>
    public T? Get<T>(string key)
    {
        key = ConvertKeyToFilePath(key);//this must happen first!
        T? cacheEntry = default;
        var isExpired = TryGetExpiration(key, out var slidingExpiration, out var absoluteExpiration);
        var exists = File.Exists(key);
        if (exists && isExpired)
        {
            File.Delete(key);
            _slidingExpirations.TryRemove(key, out var _);
            _absoluteExpirations.TryRemove(key, out var _);
            _logger.LogTrace("{className} retrieved object with {key} but deleted it due to expiration", nameof(DiskCacheService), key);
        }
        else if (exists)
        {
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
            UpdateExpirations(key, ref slidingExpiration, ref absoluteExpiration);
            _logger.LogTrace("{className} retrieved object {objectType} with {key}",
                nameof(DiskCacheService), typeof(T), key);
        }
        else
            _logger.LogTrace("{className} retrieved object {objectType} with {key} failed",
                nameof(DiskCacheService), typeof(T), key);
        return cacheEntry;
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T cacheEntry, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null)
    {
        key = ConvertKeyToFilePath(key);//this must happen first!
        DiskCacheService.ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        _logger.LogTrace("{className} attempting to store object with {key}", nameof(DiskCacheService), key);
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
            UpdateExpirations(key, ref slidingExpiration, ref absoluteExpiration);
        }
    }

    private static void ValidateExpirations(string key, TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration = null)
    {
        if (slidingExpiration.HasValue && absoluteExpiration.HasValue)
            throw new NotSupportedException($"{nameof(slidingExpiration)} and {nameof(absoluteExpiration)} are both requested for key {key}!");
        if (absoluteExpiration.HasValue && absoluteExpiration.Value < DateTime.UtcNow)
            throw new NotSupportedException($"{nameof(absoluteExpiration)} is requested for key {key} but {absoluteExpiration} is already expired!");
    }

    private void UpdateExpirations(string key, ref TimeSpan? slidingExpiration, ref DateTimeOffset? absoluteExpiration)
    {
        if (slidingExpiration.HasValue)
        {
            var _slidingExpiration = slidingExpiration.Value;//because can't use ref type in lamba
            _ = _slidingExpirations.AddOrUpdate(key, _slidingExpiration, (k, v) => { v = _slidingExpiration; return v; });
            if (!absoluteExpiration.HasValue)
                absoluteExpiration = DateTime.UtcNow + _slidingExpiration;
        }
        if (absoluteExpiration.HasValue)
        {
            var _absoluteExpiration = absoluteExpiration.Value;//because can't use ref type in lamba
            _ = _absoluteExpirations.AddOrUpdate(key, _absoluteExpiration, (k, v) => { v = _absoluteExpiration; return v; });
        }
    }

    private bool TryGetExpiration(string key, out TimeSpan? slidingExpiration, out DateTimeOffset? absoluteExpiration)
    {
        slidingExpiration = null;
        if (_slidingExpirations.TryGetValue(key, out var sExpiration))
            slidingExpiration = sExpiration;

        absoluteExpiration = null;
        if (_absoluteExpirations.TryGetValue(key, out var aExpiration))
            absoluteExpiration = aExpiration;

        return absoluteExpiration.HasValue && absoluteExpiration.Value.DateTime < DateTime.UtcNow;
    }

    /// <summary>
    /// Converts a Redis cache key into a valid file path.
    /// </summary>
    /// <remarks>
    /// TODO: need to use more comprehensive regex here for the instead of search/replace.
    /// </remarks>
    private string ConvertKeyToFilePath(string key)
    {
        if (string.IsNullOrWhiteSpace(_diskCacheFolder))
            throw new ArgumentException($"to use {nameof(DiskCacheService)} you must set the {nameof(_cachingOptions.DiskCacheFolder)}");
        return Path.Combine(_diskCacheFolder, key.Replace(":", "_"));
    }

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "not yet plugged into the interface")]
    public async Task<T?> GetAsync<T>(string key, Func<Task<T>>? createItem = null, CancellationToken token = default) where T : class
    {
        key = ConvertKeyToFilePath(key);
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

    /// <inheritdoc/>
    public bool Delete(string key)
    {
        key = ConvertKeyToFilePath(key);
        if (File.Exists(key))
        {
            File.Delete(key);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public long DeleteAll()
    {
        var di = new DirectoryInfo(_diskCacheFolder);
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
}
