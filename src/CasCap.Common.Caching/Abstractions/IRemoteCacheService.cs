namespace CasCap.Abstractions;

public interface IRemoteCacheService
{
    IConnectionMultiplexer Connection { get; }
    IDatabase Db { get; }
    ISubscriber Subscriber { get; }
    IServer Server { get; }
    int DatabaseId { get; }

    string? Get(string key, CommandFlags flags = CommandFlags.None);
    byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None);
    Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None);
    Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None);

    bool Set(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None);
    bool Set(string key, string value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None);
    Task<bool> SetAsync(string key, byte[] value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None);
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None);

    bool Delete(string key, CommandFlags flags = CommandFlags.None);
    Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None);

    Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithTTL<T>(string key);
    Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithTTL_Lua<T>(string key, [CallerMemberName] string caller = "");

    Dictionary<string, LoadedLuaScript> LuaScripts { get; set; }
    bool LoadLuaScript(Assembly assembly, string scriptName);
}
