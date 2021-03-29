using CasCap.Common.Extensions;
using CasCap.Interfaces;
using CasCap.Logic;
using CasCap.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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

        Task Delete(string key);
        void DeleteLocal(string key, bool viaPubSub = false);
    }

    /// <summary>
    /// Distributed cache service uses both a local in-memory cache AND a remote Redis cache.
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
        //public ConcurrentDictionary<string, object> dItems { get; set; } = new();

        public Task<T?> Get<T>(ICacheKey<T> key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
            => Get(key.CacheKey, createItem, ttl);

        public async Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, int ttl = -1) where T : class
        {
            if (!_local.TryGetValue(key, out T cacheEntry))
            {
                var tpl = await _redis.GetCacheEntryWithTTL<T>(key);
                if (tpl != default)
                {
                    _logger.LogTrace("retrieved {key} object type {type} from redis cache", key, typeof(T));
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
                        _logger.LogTrace("attempting to set {key} object type {type} in local cache", key, typeof(T));
                        if (cacheEntry != null)
                        {
                            await Set(key, cacheEntry, ttl);
                        }
                    }
                }
            }
            else
                _logger.LogTrace("retrieved {key} object type {type} from local cache", key, typeof(T));
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
            _ = _local.Set(key, cacheEntry, options);
            _logger.LogTrace("set {key} in local cache", key);
        }

        public async Task Delete(string key)
        {
            DeleteLocal(key, false);
            _ = await _redis.DeleteAsync(key, CommandFlags.FireAndForget);
            _redis.subscriber.Publish(_cachingConfig.ChannelName, key, CommandFlags.FireAndForget);
            _logger.LogDebug("removed {key} from local+remote cache and also from the local cache of any subscriber", key);
        }

        public void DeleteLocal(string key, bool viaPubSub)
        {
            _local.Remove(key);
            if (viaPubSub)
                _logger.LogDebug("removed {key} from local cache via pub/sub", key);
        }

        void EvictionCallback(object key, object value, EvictionReason reason, object state)
        {
            var args = new PostEvictionEventArgs(key, value, reason, state);
            OnRaisePostEvictionEvent(args);
            _logger.LogTrace("evicted {key} from local cache, reason {reason}", args.key, args.reason);
        }
    }
}