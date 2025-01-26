namespace CasCap.Services;

/// <summary>
/// The <see cref="RedisCacheService"/> acts as a wrapper around key functionality of the <see cref="StackExchange.Redis"/> library.
/// </summary>
public class RedisCacheService : IRemoteCache
{
    private readonly ILogger _logger;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly CachingOptions _cachingOptions;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public IConnectionMultiplexer Connection { get { return _connectionMultiplexer; } }

    /// <inheritdoc/>
    public IDatabase Db { get { return Connection.GetDatabase(DatabaseId); } }

    /// <inheritdoc/>
    public ISubscriber Subscriber { get { return Connection.GetSubscriber(); } }

    /// <inheritdoc/>
    public IServer Server { get { return Connection.GetServer(_connectionMultiplexer.GetEndPoints()[0]); } }

    /// <inheritdoc/>
    public int DatabaseId { get; set; } = -1;

    /// <summary>
    /// Collection keeps track of the cache item requested sliding expirations.
    /// When we attempt to a previously cached item we also send in the sliding
    /// expiration again to push the Redis expiration forward.
    /// </summary>
    /// <remarks>
    /// TODO: this is currently a memory leak as there is no way to remove expired cache entries.
    /// </remarks>
    private ConcurrentDictionary<string, TimeSpan> _slidingExpirations = [];

    /// <summary>
    /// Delete all items in the Redis database.
    /// </summary>
    /// <remarks>
    /// Only works when connecting with ADMIN=true.
    /// </remarks>
    private void DeleteAll(CommandFlags flags = CommandFlags.None)
        => Server.FlushDatabase(DatabaseId, flags);

    /// <inheritdoc/>
    public string? Get(string key, CommandFlags flags = CommandFlags.None)
        => _Get(key, flags);

    /// <inheritdoc/>
    public byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None)
        => (byte[]?)_Get(key, flags);

    private RedisValue _Get(string key, CommandFlags flags = CommandFlags.None)
        => TryGetExpiration(key, out var slidingExpiration)
            ? Db.StringGetSetExpiry(key, slidingExpiration, flags)
            : Db.StringGet(key, flags);

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None)
        => await _GetAsync(key, flags);

    /// <inheritdoc/>
    public async Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None)
        => (byte[]?)(await _GetAsync(key, flags));

    private async Task<RedisValue> _GetAsync(string key, CommandFlags flags = CommandFlags.None)
        => TryGetExpiration(key, out var slidingExpiration)
            ? await Db.StringGetSetExpiryAsync(key, slidingExpiration, flags)
            : await Db.StringGetAsync(key, flags);

    /// <inheritdoc/>
    public bool Set(string key, string value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None)
    {
        ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        UpdateExpirations(key, ref slidingExpiration, absoluteExpiration);
        return Db.StringSet(key, value, slidingExpiration, flags: flags);
    }

    /// <inheritdoc/>
    public bool Set(string key, byte[] value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None)
    {
        ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        UpdateExpirations(key, ref slidingExpiration, absoluteExpiration);
        return Db.StringSet(key, value, slidingExpiration, flags: flags);
    }

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, string value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None)
    {
        ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        UpdateExpirations(key, ref slidingExpiration, absoluteExpiration);
        return Db.StringSetAsync(key, value, slidingExpiration, flags: flags);
    }

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None)
    {
        ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        UpdateExpirations(key, ref slidingExpiration, absoluteExpiration);
        return Db.StringSetAsync(key, value, slidingExpiration, flags: flags);
    }

    private void ValidateExpirations(string key, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null)
    {
        if (slidingExpiration.HasValue && absoluteExpiration.HasValue)
            throw new NotSupportedException($"{nameof(slidingExpiration)} and {nameof(absoluteExpiration)} are both requested for key {key}!");
        if (absoluteExpiration.HasValue && absoluteExpiration.Value < DateTime.UtcNow)
            throw new NotSupportedException($"{nameof(absoluteExpiration)} is requested for key {key} but {absoluteExpiration} is already expired!");
    }

    private void UpdateExpirations(string key, ref TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration = null)
    {
        if (slidingExpiration.HasValue)
        {
            var _slidingExpiration = slidingExpiration.Value;//because can't use ref type in lamba
            _ = _slidingExpirations.AddOrUpdate(key, _slidingExpiration, (k, v) => { v = _slidingExpiration; return v; });
        }
        //Redis doesn't support absolute expiration, so we convert any given absoluteExpiration
        //into a relative value - but we don't add the key to the _slidingExpirations collection.
        if (absoluteExpiration.HasValue)
            slidingExpiration = absoluteExpiration.Value - DateTime.UtcNow;
    }

    private bool TryGetExpiration(string key, out TimeSpan? slidingExpiration)
    {
        slidingExpiration = null;
        if (_slidingExpirations.TryGetValue(key, out var sExpiration))
            slidingExpiration = sExpiration;
        return slidingExpiration.HasValue;
    }

    /// <inheritdoc/>
    public bool Delete(string key, CommandFlags flags = CommandFlags.None) => Db.KeyDelete(key, flags);

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None) => Db.KeyDeleteAsync(key, flags);

    /// <inheritdoc/>
    public async Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithTTL<T>(string key, CommandFlags flags = CommandFlags.None)
    {
        (TimeSpan? expiry, T? cacheEntry) tpl = default;
        var o = await Db.StringGetWithExpiryAsync(key, flags);
        if (o.Value.HasValue)
        {
            _logger.LogTrace("{className} retrieved {key} object type {type} from remote cache",
                nameof(RedisCacheService), key, typeof(T));
            if (_cachingOptions.RemoteCache.SerializationType == SerializationType.Json)
            {
                var json = o.Value.ToString();
                tpl.cacheEntry = json.FromJson<T>();
            }
            else if (_cachingOptions.RemoteCache.SerializationType == SerializationType.MessagePack)
            {
                var bytes = (byte[])o.Value!;
                tpl.cacheEntry = bytes.FromMessagePack<T>();
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.RemoteCache.SerializationType)} {_cachingOptions.RemoteCache.SerializationType} is not supported!");
            if (o.Expiry.HasValue)
                tpl.expiry = o.Expiry;
        }
        else
            _logger.LogTrace("{className} unable to retrieve {key} object type {type} from remote cache",
                nameof(RedisCacheService), key, typeof(T));
        return tpl;
    }

    /// <inheritdoc/>
    public async Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithTTL_Lua<T>(string key, CommandFlags flags = CommandFlags.None, [CallerMemberName] string caller = "")
    {
        if (!_cachingOptions.LoadBuiltInLuaScripts)
            throw new NotSupportedException($"You must enable {nameof(_cachingOptions.LoadBuiltInLuaScripts)} to execute this method!");

        (TimeSpan? expiry, T? cacheEntry) tpl = default;

        var res = await luaGet();
        if (res != default && res.payload is not null)
        {
            tpl.expiry = res.ttl.GetExpirationFromSeconds();
            if (_cachingOptions.RemoteCache.SerializationType == SerializationType.Json)
            {
                var json = (string?)res.payload;
                if (json is not null)
                    tpl.cacheEntry = json.FromJson<T>();
            }
            else if (_cachingOptions.RemoteCache.SerializationType == SerializationType.MessagePack)
            {
                var bytes = (byte[]?)res.payload;
                if (bytes is not null)
                    tpl.cacheEntry = bytes.FromMessagePack<T>();
            }
            else
                throw new NotSupportedException($"{nameof(_cachingOptions.RemoteCache.SerializationType)} {_cachingOptions.RemoteCache.SerializationType} is not supported!");
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
            TryGetExpiration(key, out var slidingExpiration);
            var cacheExpiry = slidingExpiration.HasValue ? (int)slidingExpiration.Value.TotalSeconds : -1;
            try
            {
                var luaScript = LuaScripts[keyGetCacheEntryWithTTL];
                var result = await luaScript.EvaluateAsync(Db, new
                {
                    cacheKey = (RedisKey)key,//the key of the item we wish to retrieve
                    cacheExpiry = (RedisKey)cacheExpiry.ToString(),
                    trackKey = (RedisKey)GetTrackKey(),//the key of the HashSet recording access attempts (expiry set to 7 days)
                    trackCaller = (RedisKey)caller//the method which instigated this particular access attempt
                }, flags: flags);
                var retKeys = (RedisResult[]?)result;
                return retKeys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{className} some failure", nameof(RedisCacheService));
                throw;
            }

            //edits the item cacheKey by appending the date.
            string GetTrackKey()
            {
                var lIndex = key.LastIndexOf(':');
                if (lIndex > -1) key = key.Substring(0, lIndex);
                key = $"{key}:{DateTime.UtcNow:yyyy-MM-dd}";
                return key;
            }
        }
    }

    #region
    /// <inheritdoc/>
    public Dictionary<string, LoadedLuaScript> LuaScripts { get; set; } = [];

    private void LoadBuiltInLuaScripts()
    {
        //add additional default LUA scripts into this array...
        var scriptNames = new[] { keyGetCacheEntryWithTTL };
        foreach (var scriptName in scriptNames)
            LoadLuaScript(GetType().Assembly, scriptName);
    }

    private const string keyGetCacheEntryWithTTL = nameof(GetCacheEntryWithTTL);

    /// <inheritdoc/>
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
        _logger.LogTrace("{className} loading Lua script '{scriptName}'", nameof(RedisCacheService), resourceName);
        var loadedLuaScript = luaScript.Load(Server);

        return LuaScripts.TryAdd(scriptName, loadedLuaScript);
    }
    #endregion
}
