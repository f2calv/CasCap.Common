﻿using StackExchange.Redis;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CasCap.Abstractions;

public interface IRemoteCacheService
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
