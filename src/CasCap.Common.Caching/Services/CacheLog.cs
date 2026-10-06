namespace CasCap.Common.Services;

/// <summary>Source-generated logging for caching services.</summary>
internal static partial class CacheLog
{
    /// <summary>Logs a cache service lifecycle transition.</summary>
    [LoggerMessage(LogLevel.Information, "{ClassName} {State}")]
    internal static partial void Lifecycle(ILogger logger, string className, string state);

    /// <summary>Logs a cache lookup result.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} {Result} {Key} object type {ObjectType} from {ObjectName}")]
    internal static partial void CacheLookup(
        ILogger logger,
        string className,
        string result,
        string key,
        Type objectType,
        string objectName);

    /// <summary>Logs a cache store operation.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} storing {Key} object type {ObjectType} in {ObjectName}")]
    internal static partial void CacheStore(ILogger logger, string className, string key, Type objectType, string objectName);

    /// <summary>Logs a cache-key operation.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} {Action} cache entry {Key}")]
    internal static partial void CacheEntryOperation(ILogger logger, string className, string action, string key);

    /// <summary>Logs a disk-cache expiration.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} retrieved object with {Key} but deleted it due to expiration")]
    internal static partial void DiskEntryExpired(ILogger logger, string className, string key);

    /// <summary>Logs a disk-cache deserialization failure.</summary>
    [LoggerMessage(LogLevel.Error, "{ClassName} deserialization error for {Key}")]
    internal static partial void DeserializationError(ILogger logger, Exception exception, string className, string key);

    /// <summary>Logs deletion of a file-system cache item.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} attempting deletion of {ItemType} {ItemName}")]
    internal static partial void FileSystemDeletion(ILogger logger, string className, string itemType, string itemName);

    /// <summary>Logs a memory-cache store operation.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} stored {ObjectType} with {Key} in {ApiName} (options {@Options})")]
    internal static partial void MemoryStored(
        ILogger logger,
        string className,
        Type objectType,
        string key,
        string apiName,
        object options);

    /// <summary>Logs a memory-cache eviction.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} evicted object with {Key} from {ApiName} (reason {Reason})")]
    internal static partial void MemoryEvicted(
        ILogger logger,
        string className,
        string apiName,
        object key,
        EvictionReason reason);

    /// <summary>Logs a memory-cache deletion result.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} {Result} object with {Key} from {ApiName}")]
    internal static partial void MemoryDeleted(ILogger logger, string className, string result, string key, string apiName);

    /// <summary>Logs a Redis channel subscription transition.</summary>
    [LoggerMessage(LogLevel.Debug, "{ClassName} {Action} {ObjectType} name {ChannelName}, {PropertyName}={IsPattern}")]
    internal static partial void ChannelSubscription(
        ILogger logger,
        string className,
        string action,
        Type objectType,
        string channelName,
        string propertyName,
        bool isPattern);

    /// <summary>Logs local cache invalidation by another client.</summary>
    [LoggerMessage(LogLevel.Debug, "{ClassName} cache key {Key} was invalidated by client {ClientName} and removed from {AbstractionName}")]
    internal static partial void CacheInvalidated(
        ILogger logger,
        string className,
        string key,
        string clientName,
        string abstractionName);

    /// <summary>Logs a skipped local cache invalidation.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} skipped removing {Key} from {AbstractionName} because this instance raised the event")]
    internal static partial void CacheInvalidationSkipped(ILogger logger, string className, string key, string abstractionName);

    /// <summary>Logs a failed distributed-lock acquisition.</summary>
    [LoggerMessage(LogLevel.Warning, "{ClassName} failed to acquire distributed lock for {Key}")]
    internal static partial void DistributedLockFailure(ILogger logger, string className, string key);

    /// <summary>Logs publication of a local-cache expiration message.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} sent {AbstractionName} expiration message for {Key} via pub/sub")]
    internal static partial void ExpirationMessageSent(ILogger logger, string className, string abstractionName, string key);

    /// <summary>Logs Redis expiration housekeeping.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} expiration detected key={Key}, removal status={Success}, {Count} item(s) remaining")]
    internal static partial void ExpirationDetected(ILogger logger, string className, string key, bool success, int count);

    /// <summary>Logs a Redis Lua execution failure.</summary>
    [LoggerMessage(LogLevel.Error, "{ClassName} Lua execution failed")]
    internal static partial void LuaFailure(ILogger logger, Exception exception, string className);

    /// <summary>Logs loading a Redis Lua script.</summary>
    [LoggerMessage(LogLevel.Trace, "{ClassName} loading Lua script {ScriptName}")]
    internal static partial void LuaScriptLoading(ILogger logger, string className, string scriptName);

    /// <summary>Logs a duplicate Redis Lua script name.</summary>
    [LoggerMessage(LogLevel.Warning, "{ClassName} loading Lua script {ScriptName} failed, duplicate name found")]
    internal static partial void DuplicateLuaScript(ILogger logger, string className, string scriptName);
}
