using CasCap.Common.Extensions;
using CasCap.Interfaces;
using CasCap.Logic;
using CasCap.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
namespace CasCap.Services
{
    public interface IDistCacheService
    {
        event EventHandler<PostEvictionEventArgs> PostEvictionEvent;

        //todo:create 2x overload options to accept ttl(expiry) of a utc datetime
        Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class;
        Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class;

        Task Set<T>(string key, T cacheEntry, int ttl = -1) where T : class;
        Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class;

        Task Remove(string key);
    }

    /// <summary>
    /// Distributed cached service using a local in-memory cache AND remote Redis cache server.
    /// </summary>
    public class DistCacheService : IDistCacheService
    {
        readonly ILogger _logger;
        readonly CachingConfig _cachingConfig;
        readonly IRedisCacheService _redis;
        readonly IMemoryCache _local;

        public event EventHandler<PostEvictionEventArgs>? PostEvictionEvent;
        protected virtual void OnRaisePostEvictionEvent(PostEvictionEventArgs args) { PostEvictionEvent?.Invoke(this, args); }

        public DistCacheService(ILogger<DistCacheService> logger,
            IOptions<CachingConfig> cachingConfig,
            IRedisCacheService redis//,
                                    //IMemoryCache local
            )
        {
            _logger = logger;
            _cachingConfig = cachingConfig.Value;
            _redis = redis;
            //_local = local;
            //todo:consider a Flags to disable use of local/shared memory in (console) applications that don't need it?
            _local = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = _cachingConfig.MemoryCacheSizeLimit,
            });
        }

        readonly AsyncDuplicateLock locker = new();

        //todo:store a summary of all cached items in a local lookup dictionary?
        //public ConcurrentDictionary<string, object> dItems { get; set; } = new ConcurrentDictionary<string, object>();

        public Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
            => Get<T>(key.CacheKey, createItem, ttl);

        public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
        {
            if (!_local.TryGetValue<T>(key, out T cacheEntry))
            {
                var tpl = await _redis.GetCacheEntryWithTTL<T>(key);
                if (tpl != default)
                {
                    _logger.LogTrace($"{key}\tretrieved cacheEntry object {typeof(T)} from redis cache");
                    cacheEntry = tpl.cacheEntry;
                    SetLocal(key, cacheEntry, tpl.expiry);
                }
                else if (createItem != null)
                {
                    //if we use Func and go create the cacheEntry, then we lock here to prevent multiple creations occurring at the same time
                    //https://www.hanselman.com/blog/EyesWideOpenCorrectCachingIsAlwaysHard.aspx
                    using (await locker.LockAsync(key))
                    {
                        // Key not in cache, so get data.
                        cacheEntry = await createItem();
                        _logger.LogTrace($"{key}\tattempting to set a new cacheEntry object {typeof(T)}");
                        if (cacheEntry != null)
                        {
                            await Set(key, cacheEntry, ttl);
                        }
                    }
                }
            }
            else
                _logger.LogTrace($"{key}\tretrieved cacheEntry object {typeof(T)} from local cache");
            return cacheEntry;
        }

        public Task Set<T>(ICacheKey<T> key, T cacheEntry, int ttl = -1) where T : class
            => Set(key.CacheKey, cacheEntry, ttl);

        public async Task Set<T>(string key, T cacheEntry, int ttl = -1) where T : class
        {
            var expiry = ttl.GetExpiry();

            var bytes = cacheEntry.ToMessagePack();
            _ = await _redis.SetAsync(key, bytes, expiry);

            SetLocal(key, cacheEntry, expiry);
        }

        void SetLocal<T>(string key, T cacheEntry, TimeSpan? expiry) where T : class
        {
            var options = new MemoryCacheEntryOptions()
                // Pin to cache.
                .SetPriority(CacheItemPriority.Normal)
                // Set cache entry size by extension method.
                .SetSize(1)
                // Add eviction callback
                .RegisterPostEvictionCallback(EvictionCallback/*, this or cacheEntry.GetType()*/)
                ;
            if (expiry.HasValue)
                options.SetAbsoluteExpiration(expiry.Value);
            _local.Set<T>(key, cacheEntry, options);
            _logger.LogTrace($"{key}\tset in local cache");
        }

        public async Task Remove(string key)
        {
            await Task.Delay(0);
            _local.Remove(key);
            _ = await _redis.RemoveAsync(key);
            _logger.LogDebug($"removed key {key}");
        }

        void EvictionCallback(object key, object value, EvictionReason reason, object state)
        {
            var args = new PostEvictionEventArgs(key, value, reason, state);
            OnRaisePostEvictionEvent(args);
            var message = $"local cache entry {args.key} was evicted, reason: {args.reason}";
            _logger.LogDebug(message);
        }
    }
}