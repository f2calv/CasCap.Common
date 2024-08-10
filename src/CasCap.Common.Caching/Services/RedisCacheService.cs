using StackExchange.Redis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace CasCap.Services;

//https://stackexchange.github.io/StackExchange.Redis/
public class RedisCacheService : IRemoteCacheService
{
    readonly ILogger _logger;
    readonly IConnectionMultiplexer _connectionMultiplexer;
    readonly CachingOptions _cachingOptions;

    public RedisCacheService(ILogger<RedisCacheService> logger, IConnectionMultiplexer connectionMultiplexer, IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _connectionMultiplexer = connectionMultiplexer;
        _cachingOptions = cachingOptions.Value;
        //Note: below for getting Redis working container to container on docker compose, https://github.com/StackExchange/StackExchange.Redis/issues/1002
        //configuration.ResolveDns = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_COMPOSE"), out var _);
        if (_cachingOptions.LoadBuiltInLuaScripts) LoadBuiltInLuaScripts();
        if (_cachingOptions.RemoteCache.ClearOnStartup) DeleteAll();
    }

    public IConnectionMultiplexer Connection { get { return _connectionMultiplexer; } }

    public IDatabase Db { get { return Connection.GetDatabase(DatabaseId); } }

    public ISubscriber Subscriber { get { return Connection.GetSubscriber(); } }

    public IServer Server { get { return Connection.GetServer(_connectionMultiplexer.GetEndPoints()[0]); } }

    public int DatabaseId { get; set; } = -1;

    public void DeleteAll(CommandFlags flags = CommandFlags.None)
        => Server.FlushDatabase(DatabaseId, flags);

    public string? Get(string key, CommandFlags flags = CommandFlags.None)
        => Db.StringGet(key, flags);

    public byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None)
        => Db.StringGet(key, flags);

    public async Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None)
        => await Db.StringGetAsync(key, flags);

    public async Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None)
        => await Db.StringGetAsync(key, flags);

    public bool Set(string key, string value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None)
        => Db.StringSet(key, value, expiry, flags: flags);

    public bool Set(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None)
        => Db.StringSet(key, value, expiry, flags: flags);

    public Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None)
        => Db.StringSetAsync(key, value, expiry, flags: flags);

    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None)
        => Db.StringSetAsync(key, value, expiry, flags: flags);

    public bool Delete(string key, CommandFlags flags = CommandFlags.None) => Db.KeyDelete(key, flags);

    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None) => Db.KeyDeleteAsync(key, flags);

    public async Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL<T>(string key)
    {
        (TimeSpan? expiry, T cacheEntry) tpl = default;
        var o = await Db.StringGetWithExpiryAsync(key);
        if (o.Expiry.HasValue && o.Value.HasValue)
        {
            tpl.expiry = o.Expiry;
            if (_cachingOptions.RemoteCache.SerialisationType == SerialisationType.Json)
            {
                var json = o.Value.ToString();
                tpl.cacheEntry = json.FromJSON<T>();
            }
            else if (_cachingOptions.RemoteCache.SerialisationType == SerialisationType.MessagePack)
            {
                var bytes = (byte[])o.Value!;
                tpl.cacheEntry = bytes.FromMessagePack<T>();
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.RemoteCache.SerialisationType)} {_cachingOptions.RemoteCache.SerialisationType} is not supported!");
        }
        return tpl;
    }

    #region use custom LUA script to return cached object plus meta data i.e. object expiry information
    [Obsolete("Superseded by the built-in StringGetWithExpiryAsync, however left as a Lua script example.")]
    public async Task<(TimeSpan? expiry, T cacheEntry)> GetCacheEntryWithTTL_Lua<T>(string key, [CallerMemberName] string caller = "")
    {
        if (!_cachingOptions.LoadBuiltInLuaScripts)
            throw new NotSupportedException($"You must enable {nameof(_cachingOptions.LoadBuiltInLuaScripts)} to execute this method!");

        (TimeSpan? expiry, T cacheEntry) tpl = default;

        var res = await luaGet();
        if (res != default && res.payload is not null)
        {
            tpl.expiry = res.ttl.GetExpiry();
            if (_cachingOptions.RemoteCache.SerialisationType == SerialisationType.Json)
            {
                var json = (string?)res.payload;
                tpl.cacheEntry = json.FromJSON<T>();
            }
            else if (_cachingOptions.RemoteCache.SerialisationType == SerialisationType.MessagePack)
            {
                var bytes = (byte[]?)res.payload;
                tpl.cacheEntry = bytes.FromMessagePack<T>();
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.RemoteCache.SerialisationType)} {_cachingOptions.RemoteCache.SerialisationType} is not supported!");
        }

        return tpl;

        async Task<(int ttl, RedisResult payload)> luaGet()
        {
            (int, RedisResult) output = default;
            var tpl = await luaGetCacheEntryWithTTL();
            if (tpl != default)
                output = (tpl.ttl, tpl.payload);
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
                var result = await luaScript.EvaluateAsync(Db, new
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
                _logger.LogError(ex, "some failure");
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

    void LoadBuiltInLuaScripts()
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
        _logger.LogTrace("{serviceName} loading Lua script '{scriptName}'", nameof(RedisCacheService), resourceName);
        var loadedLuaScript = luaScript.Load(Server);

        return LuaScripts.TryAdd(scriptName, loadedLuaScript);
    }
    #endregion
    #endregion
}
