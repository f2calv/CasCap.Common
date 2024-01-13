using StackExchange.Redis;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace CasCap.Services;

public interface IRedisCacheService
{
    IConnectionMultiplexer Connection { get; }
    IDatabase db { get; }
    ISubscriber subscriber { get; }
    IServer server { get; }

    byte[]? Get(string key, CommandFlags flags = CommandFlags.None);
    Task<byte[]?> GetAsync(string key, CommandFlags flags = CommandFlags.None);

    bool Set(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None);
    Task<bool> SetAsync(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None);

    bool Delete(string key, CommandFlags flags = CommandFlags.None);
    Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None);

    Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL<T>(string key);
    Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL_Lua<T>(string key, [CallerMemberName] string caller = "");

    Dictionary<string, LoadedLuaScript> LuaScripts { get; set; }
    bool LoadLuaScript(Assembly assembly, string scriptName);
}

//https://stackexchange.github.io/StackExchange.Redis/
public class RedisCacheService : IRedisCacheService
{
    readonly ILogger _logger;
    readonly CachingOptions _cachingOptions;

    public RedisCacheService(ILogger<RedisCacheService> logger, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _cachingOptions = cachingOptions.Value;
        configurationOptions = ConfigurationOptions.Parse(_cachingOptions.redisConnectionString);
        //configurationOptions.ConnectRetry = 20;
        configurationOptions.ClientName = $"{AppDomain.CurrentDomain.FriendlyName}-{Environment.MachineName}";
        //Note: below for getting Redis working container to container on docker compose, https://github.com/StackExchange/StackExchange.Redis/issues/1002
        //configuration.ResolveDns = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_COMPOSE"), out var _);

        LoadDefaultLuaScripts();
    }

    static ConfigurationOptions configurationOptions { get; set; } = new();

    readonly Lazy<ConnectionMultiplexer> LazyConnection = new(() => ConnectionMultiplexer.Connect(configurationOptions));

    public IConnectionMultiplexer Connection { get { return LazyConnection.Value; } }

    public IDatabase db { get { return Connection.GetDatabase(); } }

    public ISubscriber subscriber { get { return Connection.GetSubscriber(); } }

    public IServer server { get { return Connection.GetServer(configurationOptions.EndPoints[0]); } }

    public byte[]? Get(string key, CommandFlags flags = CommandFlags.None) => db.StringGet(key, flags);

    public async Task<byte[]?> GetAsync(string key, CommandFlags flags = CommandFlags.None) => await db.StringGetAsync(key, flags);

    public bool Set(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None)
        => db.StringSet(key, value, expiry, flags: flags);

    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None)
        => db.StringSetAsync(key, value, expiry, flags: flags);

    public bool Delete(string key, CommandFlags flags = CommandFlags.None) => db.KeyDelete(key, flags);

    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None) => db.KeyDeleteAsync(key, flags);

    public async Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL<T>(string key)
    {
        (TimeSpan? expiry, T cacheEntry) tpl = default;
        var o = await db.StringGetWithExpiryAsync(key);
        if (o.Expiry.HasValue && o.Value.HasValue)
        {
            var requestedObject = ((byte[])o.Value)!.FromMessagePack<T>();
            return (o.Expiry, requestedObject);
        }
        else
            return tpl;
    }

    #region use custom LUA script to return cached object plus meta data i.e. object expiry information
    [Obsolete("Superseded by the built-in StringGetWithExpiryAsync, however left as a Lua script example.")]
    public async Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL_Lua<T>(string key, [CallerMemberName] string caller = "")
    {
        (TimeSpan?, T) res = default;

        //handle binary format
        var tpl = await luaGetBytes();
        if (tpl != default && tpl.bytes is not null)
        {
            var requestedObject = tpl.bytes.FromMessagePack<T>();
            var expiry = tpl.ttl.GetExpiry();
            res = (expiry, requestedObject);
        }
        return res;

        async Task<(int ttl, byte[]? bytes)> luaGetBytes()
        {
            (int, byte[]?) output = default;
            var tpl = await luaGetCacheEntryWithTTL();
            if (tpl != default)
                output = (tpl.ttl, (byte[]?)tpl.payload);
            return output;
        }

        async Task<(int ttl, string type, RedisResult payload)> luaGetCacheEntryWithTTL()
        {
            (int, string, RedisResult) tpl = default;
            var retKeys = await GetCacheEntryWithTTL();
            if (retKeys is not null && retKeys.Length == 3)
            {
                var ttl = retKeys[0] is not null ? (int)retKeys[0] : -1;
                var type = retKeys[1] is not null ? (string)retKeys[1]! : string.Empty;
                tpl = (int.Parse(ttl.ToString()), type.ToString(), retKeys[2]);
            }
            return tpl;
        }

        //Retrieves both the TTL and the cached item from Redis in one network call.
        async Task<RedisResult[]?> GetCacheEntryWithTTL()
        {
            try
            {
                var luaScript = LuaScripts[keyGetCacheEntryWithTTL];
                var result = await luaScript.EvaluateAsync(db, new
                {
                    cacheKey = (RedisKey)key,//the key of the item we wish to retrieve
                    trackKey = (RedisKey)GetTrackKey(),//the key of the HashSet recording access attempts (expiry set to 7 days)
                    trackCaller = (RedisKey)caller//the method which instigated this particular access attempt
                });
                var retKeys = (RedisResult[]?)result;
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

    #region
    public Dictionary<string, LoadedLuaScript> LuaScripts { get; set; } = new();

    void LoadDefaultLuaScripts()
    {
        //add additional default LUA scripts into this array...
        var scriptNames = new[] { keyGetCacheEntryWithTTL };
        foreach (var scriptName in scriptNames)
            LoadLuaScript(this.GetType().Assembly, scriptName);
    }

    const string keyGetCacheEntryWithTTL = nameof(GetCacheEntryWithTTL);

    public bool LoadLuaScript(Assembly assembly, string scriptName)
    {
        var resourceName = scriptName.EndsWith(".lua") ? scriptName : $"CasCap.Resources.{scriptName}.lua";
        var script = string.Empty;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                script = reader.ReadToEnd();
            }
        }

        var luaScript = LuaScript.Prepare(script);
        _logger.LogDebug("{serviceName}: loading Lua script '{scriptName}'", nameof(RedisCacheService), resourceName);
        var loadedLuaScript = luaScript.Load(server);

        return LuaScripts.TryAdd(scriptName, loadedLuaScript);
    }
    #endregion
    #endregion
}