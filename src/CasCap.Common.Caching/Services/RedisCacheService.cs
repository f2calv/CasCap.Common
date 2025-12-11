namespace CasCap.Common.Services;

/// <summary>
/// The <see cref="RedisCacheService"/> acts as a wrapper around key functionality of the <see cref="StackExchange.Redis"/> library.
/// </summary>
public class RedisCacheService : IRemoteCache
{
    private readonly ILogger _logger;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly CachingOptions _cachingOptions;

    /// <inheritdoc/>
    public RedisCacheService(ILogger<RedisCacheService> logger, IConnectionMultiplexer connectionMultiplexer,
        IOptions<CachingOptions> cachingOptions)
    {
        _logger = logger;
        _connectionMultiplexer = connectionMultiplexer;
        _cachingOptions = cachingOptions.Value;
        //Note: below for getting Redis working container to container on docker compose, https://github.com/StackExchange/StackExchange.Redis/issues/1002
        //configuration.ResolveDns = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_COMPOSE"), out var _);
        if (_cachingOptions.UseBuiltInLuaScripts) LoadBuiltInLuaScripts();
        if (_cachingOptions.RemoteCache.ClearOnStartup) DeleteAll();
    }

    /// <inheritdoc/>
    public IConnectionMultiplexer Connection { get { return _connectionMultiplexer; } }

    /// <inheritdoc/>
    public IDatabase Db { get { return Connection.GetDatabase(_cachingOptions.RemoteCache.DatabaseId); } }

    /// <inheritdoc/>
    public ISubscriber Subscriber { get { return Connection.GetSubscriber(); } }

    /// <inheritdoc/>
    public IServer Server { get { return Connection.GetServer(_connectionMultiplexer.GetEndPoints()[0]); } }

    /// <inheritdoc/>
    public ConcurrentDictionary<string, TimeSpan> SlidingExpirations { get; set; } = [];

    /// <summary>
    /// Delete all items in the Redis database.
    /// </summary>
    /// <remarks>
    /// Only works when connecting with ADMIN=true.
    /// </remarks>
    private void DeleteAll(CommandFlags flags = CommandFlags.None)
        => Server.FlushDatabase(_cachingOptions.RemoteCache.DatabaseId, flags);

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
        return Db.StringSet(key, value, slidingExpiration, false, flags: flags);
    }

    /// <inheritdoc/>
    public bool Set(string key, byte[] value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None)
    {
        ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        UpdateExpirations(key, ref slidingExpiration, absoluteExpiration);
        return Db.StringSet(key, value, slidingExpiration, false, flags: flags);
    }

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, string value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None)
    {
        ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        UpdateExpirations(key, ref slidingExpiration, absoluteExpiration);
        return Db.StringSetAsync(key, value, slidingExpiration, false, flags: flags);
    }

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None)
    {
        ValidateExpirations(key, slidingExpiration, absoluteExpiration);
        UpdateExpirations(key, ref slidingExpiration, absoluteExpiration);
        return Db.StringSetAsync(key, value, slidingExpiration, false, flags: flags);
    }

    /// <inheritdoc/>
    public Task<bool> ExtendSlidingExpirationAsync(string key, CommandFlags flags = CommandFlags.FireAndForget)
    {
        if (TryGetExpiration(key, out var slidingExpiration))
            return Db.KeyExpireAsync(key, slidingExpiration, flags: flags);
        return Task.FromResult(false);
    }

    private static void ValidateExpirations(string key, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null)
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
            _ = SlidingExpirations.AddOrUpdate(key, _slidingExpiration, (k, v) => { v = _slidingExpiration; return v; });
        }
        //Redis doesn't support absolute expiration, so we convert any given absoluteExpiration
        //into a relative value - but we don't add the key to the _slidingExpirations collection.
        if (absoluteExpiration.HasValue)
            slidingExpiration = absoluteExpiration.Value - DateTime.UtcNow;
    }

    private bool TryGetExpiration(string key, out TimeSpan? slidingExpiration)
    {
        slidingExpiration = null;
        if (SlidingExpirations.TryGetValue(key, out var sExpiration))
            slidingExpiration = sExpiration;
        return slidingExpiration.HasValue;
    }

    /// <inheritdoc/>
    public bool Delete(string key, CommandFlags flags = CommandFlags.None)
    {
        SlidingExpirations.TryRemove(key, out var _);
        return Db.KeyDelete(key, flags);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None)
    {
        SlidingExpirations.TryRemove(key, out var _);
        return Db.KeyDeleteAsync(key, flags);
    }

    /// <inheritdoc/>
    public async Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithExpiryAsync<T>(
        string key, CommandFlags flags = CommandFlags.None, bool updateSlidingExpirationIfExists = true, [CallerMemberName] string caller = "")
    {
        (TimeSpan? expiry, T? cacheEntry) tpl = default;

        RedisValueWithExpiry o;
        if (updateSlidingExpirationIfExists && TryGetExpiration(key, out var slidingExpiration) && slidingExpiration.HasValue)
        {
            if (_cachingOptions.UseBuiltInLuaScripts)
                o = await StringGetSetExpiryAsync();
            else
            {
                var _o = await Db.StringGetSetExpiryAsync(key, slidingExpiration.Value, flags);
                o = new RedisValueWithExpiry(_o, slidingExpiration.Value);
            }
        }
        else
            o = await Db.StringGetWithExpiryAsync(key, flags);
        if (o.Value.HasValue)
        {
            _logger.LogTrace("{ClassName} retrieved object {ObjectType} with {Key}",
                nameof(RedisCacheService), typeof(T), key);
            if (_cachingOptions.RemoteCache.SerializationType == SerializationType.Json)
            {
                var json = o.Value.ToString()!;
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
            _logger.LogTrace("{ClassName} retrieved object {ObjectType} with {Key} failed",
                nameof(RedisCacheService), typeof(T), key);

        return tpl;

        //custom Lua version of Db.StringGetSetExpiryAsync
        async Task<RedisValueWithExpiry> StringGetSetExpiryAsync()
        {
            RedisValueWithExpiry result = default;
            var retKeys = await StringGetWithExpiryAsyncLua();
            if (retKeys is not null && retKeys.Length == 3)
            {
                var expiry = retKeys[0] is not null ? TimeSpan.FromSeconds((int)retKeys[0]) : (TimeSpan?)null;
                //var type = retKeys[1] is not null ? (string?)retKeys[1]! : null;
                var value = retKeys[2] is not null ? (RedisValue)retKeys[2] : RedisValue.Null;
                result = new RedisValueWithExpiry(value, expiry);
            }
            return result;
        }

        //Retrieves both the TTL and the cached item from Redis in one network call.
        async Task<RedisResult[]?> StringGetWithExpiryAsyncLua()
        {
            var cacheExpiry = slidingExpiration.HasValue ? (int)slidingExpiration.Value.TotalSeconds : -1;
            try
            {
                var loaded = LuaScripts[keyStringGetSetExpiryAsync];
                if (string.IsNullOrWhiteSpace(loaded.ExecutableScript))
                    throw new FileNotFoundException($"Lua script {keyStringGetSetExpiryAsync} not found!");
                var ps = new
                {
                    cacheKey = (RedisKey)key,//the key of the item we wish to retrieve
                    cacheExpiry = (RedisValue)cacheExpiry,
                    trackKey = (RedisValue)GetTrackKey(),//the key of the HashSet recording access attempts (expiry set to 7 days)
                    trackCaller = (RedisValue)caller//the method which instigated this particular access attempt
                };
                //var result = await loaded.EvaluateAsync(Db, ps, flags: flags);
                var result = await Db.ScriptEvaluateAsync(loaded, ps, flags: flags);
                var retKeys = (RedisResult[]?)result;
                return retKeys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ClassName} some failure", nameof(RedisCacheService));
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
        //TODO: add additional Lua script examples into this array as and when required
        var resourceNames = new[] { keyStringGetSetExpiryAsync };
        foreach (var resourceName in resourceNames)
            LoadLuaScript(GetType().Assembly, resourceName);
    }

    private const string keyStringGetSetExpiryAsync = $"CasCap.Common.Resources.{nameof(Db.StringGetSetExpiryAsync)}.lua";

    /// <inheritdoc/>
    public LoadedLuaScript? LoadLuaScript(Assembly assembly, string scriptName)
    {
        var script = string.Empty;
        using (var stream = assembly.GetManifestResourceStream(scriptName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                script = reader.ReadToEnd();
            }
        }

        if (string.IsNullOrWhiteSpace(script))
            throw new GenericException($"Lua script '{scriptName}' is null or empty");

        var prepared = LuaScript.Prepare(script);
        _logger.LogTrace("{ClassName} loading Lua script '{ScriptName}'", nameof(RedisCacheService), scriptName);
        var loaded = prepared.Load(Server);
#if NET8_0_OR_GREATER
        if (!LuaScripts.TryAdd(scriptName, loaded))
#else
        if (!LuaScripts.ContainsKey(scriptName))
            LuaScripts.Add(scriptName, loaded);
        else
#endif
            _logger.LogWarning("{ClassName} loading Lua script '{ScriptName}' failed, duplicate name found",
                nameof(RedisCacheService), scriptName);

        return loaded;
    }
    #endregion
}
