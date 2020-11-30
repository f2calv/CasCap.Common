using CasCap.Common.Extensions;
using CasCap.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
namespace CasCap.Services
{
    public interface IRedisCacheService
    {
        Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL_Lua<T>(string key, [CallerMemberName] string caller = "");
        Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL<T>(string key, [CallerMemberName] string caller = "");
        Task<byte[]> GetAsync(string key);
        Task<bool> SetAsync(string key, byte[] value, TimeSpan? expiry = null);
        Task<bool> RemoveAsync(string key);
    }

    //https://stackexchange.github.io/StackExchange.Redis/
    public class RedisCacheService : IRedisCacheService
    {
        readonly ILogger _logger;// = ApplicationLogging.CreateLogger<RedisCacheService>();
        readonly CachingConfig _cachingConfig;

        public RedisCacheService(ILogger<RedisCacheService> logger, IOptions<CachingConfig> cachingConfig)
        {
            _logger = logger;
            _cachingConfig = cachingConfig.Value;
            //_logger.LogInformation($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}\tconnecting to redis...");
            _configurationOptions = ConfigurationOptions.Parse(_cachingConfig.redisConnectionString);
            _configurationOptions.ConnectRetry = 20;
            _configurationOptions.ClientName = Environment.MachineName;
            //Note: below for getting redis working container to container on docker compose, https://github.com/StackExchange/StackExchange.Redis/issues/1002
            _configurationOptions.ResolveDns = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_COMPOSE"), out var _);

            LuaScripts = GetLuaScripts();
        }

        static ConfigurationOptions _configurationOptions { get; set; } = new ConfigurationOptions();

        static readonly Lazy<ConnectionMultiplexer> LazyConnection = new(() => ConnectionMultiplexer.Connect(_configurationOptions));

        static ConnectionMultiplexer Connection { get { return LazyConnection.Value; } }

        static IDatabase _redis { get { return Connection.GetDatabase(); } }

        static IServer server { get { return Connection.GetServer(_configurationOptions.EndPoints[0]); } }

        public byte[] Get(string key) => _redis.StringGet(key);

        public async Task<byte[]> GetAsync(string key) => await _redis.StringGetAsync(key);

        #region use custom LUA script to return cached object plus meta data i.e. object expiry information
        public async Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL<T>(string key, [CallerMemberName] string caller = "")
        {
            (TimeSpan? expiry, T cacheEntry) tpl = default;
            var o = await _redis.StringGetWithExpiryAsync(key);
            if (o.Expiry.HasValue && o.Value.HasValue)
            {
                var requestedObject = ((byte[])o.Value).FromMessagePack<T>();
                return (o.Expiry, requestedObject);
            }
            else
                return tpl;
        }

        [Obsolete("Superceded by the built-in StringGetWithExpiryAsync, however left as a Lua script example.")]
        public async Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL_Lua<T>(string key, [CallerMemberName] string caller = "")
        {
            (TimeSpan?, T) res = default;

            //handle binary format
            var tpl = await luaGetBytes();
            if (tpl != default)
            {
                var requestedObject = tpl.bytes.FromMessagePack<T>();
                var expiry = tpl.ttl.GetExpiry();
                res = (expiry, requestedObject);
            }
            return res;

            async Task<(int ttl, byte[] bytes)> luaGetBytes()
            {
                (int, byte[]) output = default;
                var tpl = await luaGetCacheEntryWithTTL();
                if (tpl != default)
                    output = (tpl.ttl, (byte[])tpl.payload);
                return output;
            }

            async Task<(int ttl, string type, RedisResult payload)> luaGetCacheEntryWithTTL()
            {
                (int, string, RedisResult) tpl = default;
                var retKeys = await GetCacheEntryWithTTL();
                if (retKeys.Length == 3)
                {
                    var ttl = retKeys[0] is object ? (int)retKeys[0] : -1;
                    var type = retKeys[1] is object ? (string)retKeys[1] : string.Empty;
                    tpl = (int.Parse(ttl.ToString()), type.ToString(), retKeys[2]);
                }
                return tpl;
            }

            //Retrieves both the TTL and the cached item from Redis in one network call.
            async Task<RedisResult[]> GetCacheEntryWithTTL()
            {
                try
                {
                    var luaScript = LuaScripts[keyGetCacheEntryWithTTL];
                    var result = await luaScript.EvaluateAsync(_redis, new
                    {
                        cacheKey = (RedisKey)key,//the key of the item we wish to retrieve
                        trackKey = (RedisKey)GetTrackKey(),//the key of the HashSet recording access attempts (expiry set to 7 days)
                        trackCaller = (RedisKey)caller//the method which instigated this particular access attempt
                    });
                    var retKeys = (RedisResult[])result;
                    return retKeys;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                    throw;
                }

                //edits the item cacheKey by appending the date.
                string GetTrackKey()
                {
                    if (key.LastIndexOf(":") > -1)
                        key = key.Substring(0, key.LastIndexOf(":"));
                    key = $"{key}:{DateTime.UtcNow:yyyy-MM-dd}";
                    return key;
                }
            }
        }

        #region load lua scripts into dictionary
        readonly ConcurrentDictionary<string, LoadedLuaScript> LuaScripts;

        ConcurrentDictionary<string, LoadedLuaScript> GetLuaScripts()
        {
            var d = new ConcurrentDictionary<string, LoadedLuaScript>();
            d.TryAdd(keyGetCacheEntryWithTTL, GetScriptFromLocalResources($"CasCap.Resources.{keyGetCacheEntryWithTTL}.lua"));
            //add new LUA scripts in here...
            return d;
        }

        const string keyGetCacheEntryWithTTL = "GetCacheEntryWithTTL";

        LoadedLuaScript GetScriptFromLocalResources(string resourceName)
        {
            var script = string.Empty;
            var assembly = this.GetType().Assembly;
            //var resources = assembly.GetManifestResourceNames();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream is object)
                {
                    using var reader = new StreamReader(stream);
                    script = reader.ReadToEnd();
                }
            }

            var luaScript = LuaScript.Prepare(script);
            _logger.LogDebug("Connecting to redis instance {connectionString}", _cachingConfig.redisConnectionString);
            var loadedLuaScript = luaScript.Load(server);
            return loadedLuaScript;
        }
        #endregion
        #endregion

        public Task<bool> RemoveAsync(string key) => _redis.KeyDeleteAsync(key);

        public Task<bool> SetAsync(string key, byte[] value, TimeSpan? expiry = null) => _redis.StringSetAsync(key, value, expiry);
    }
}