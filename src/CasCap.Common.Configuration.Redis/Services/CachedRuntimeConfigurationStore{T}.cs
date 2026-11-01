using CasCap.Common.Abstractions;
using CasCap.Common.Configuration.Redis.Models;
using CasCap.Common.Configuration.Runtime.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasCap.Common.Configuration.Redis.Services;

/// <summary>Accelerates reads without replacing authoritative startup, validation or reconciliation.</summary>
public sealed partial class CachedRuntimeConfigurationStore<T> : IRuntimeConfigurationStore<T>, IDisposable where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 40,
    };
    private readonly ILogger<CachedRuntimeConfigurationStore<T>> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly RuntimeConfigurationCacheOptions _settings;
    private readonly IDistributedCache _cache;
    private readonly IRuntimeConfigurationStore<T> _authority;
    private readonly RuntimeConfigurationCodec<T> _codec;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextReconciliation;
    private long _highestRevision;
    private bool _initialized;

    /// <summary>Creates a scoped cache reader over an already registered durable store.</summary>
    public CachedRuntimeConfigurationStore(
        ILogger<CachedRuntimeConfigurationStore<T>> logger,
        TimeProvider timeProvider,
        RuntimeConfigurationCacheOptions settings,
        RuntimeConfigurationOptions contract,
        IDistributedCache cache,
        IRuntimeConfigurationStore<T> authority,
        RuntimeConfigurationCodec<T> codec
    )
    {
        settings.Validate();
        contract.Validate();
        _logger = logger;
        _timeProvider = timeProvider;
        _settings = settings;
        _cache = cache;
        _authority = authority;
        _codec = codec;
        var identity = JsonSerializer.Serialize(new[]
        {
            settings.AuthorityId, contract.Scope.Application, contract.Scope.Environment,
            contract.ContractId, contract.ContractVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        CacheKey = $"configuration:cache:v1:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))}";
    }

    /// <summary>Gets the non-secret, hashed key; also identifies the cached envelope's scope.</summary>
    public string CacheKey { get; }

    /// <inheritdoc/>
    public async Task<RuntimeConfigurationRevision<T>?> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            if (!_initialized || now >= _nextReconciliation)
                return await ReadAuthorityAsync(cancellationToken);
            var lifetime = _settings.MaximumAge * (0.9 + Random.Shared.NextDouble() * 0.1);
            var validUntil = now + lifetime;
            try
            {
                var json = await _cache.Get(CacheKey, async () =>
                {
                    var revision = await _authority.ReadAsync(cancellationToken);
                    return JsonSerializer.Serialize(new CachedSnapshot
                    {
                        Identity = CacheKey,
                        ValidUntilUtc = validUntil,
                        Revision = revision,
                    }, SerializerOptions);
                }, absoluteExpiration: validUntil).WaitAsync(cancellationToken);
                if (json is not null && Encoding.UTF8.GetByteCount(json) > 131_072)
                    throw new ValidationException("Cached runtime configuration exceeds the envelope size limit.");
                var snapshot = json is null ? null : JsonSerializer.Deserialize<CachedSnapshot>(json, SerializerOptions);
                if (snapshot is null || snapshot.Identity != CacheKey
                    || snapshot.ValidUntilUtc <= _timeProvider.GetUtcNow()
                    || snapshot.ValidUntilUtc > now + _settings.MaximumAge
                    || snapshot.Revision is null || snapshot.Revision.Revision < _highestRevision)
                {
                    LogCacheRejected(_logger, "StaleOrMismatchedEntry");
                    return await ReadAuthorityAsync(cancellationToken);
                }
                return Validate(snapshot.Revision);
            }
            catch (Exception exception) when (exception is RedisException or JsonException or ValidationException or OptionsValidationException)
            {
                LogCacheRejected(_logger, exception.GetType().Name);
            }
            catch (Exception exception) when (
                (exception is NotSupportedException or ArgumentOutOfRangeException)
                && validUntil <= _timeProvider.GetUtcNow())
            {
                LogCacheRejected(_logger, "CacheFillDeadlineElapsed");
            }
            return await ReadAuthorityAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RuntimeConfigurationRevision<T>?> ReadAuthorityAsync(CancellationToken cancellationToken)
    {
        var revision = await _authority.ReadAsync(cancellationToken);
        if (revision is null)
            return null;
        var validated = Validate(revision);
        _initialized = true;
        _nextReconciliation = _timeProvider.GetUtcNow() + _settings.ReconciliationInterval;
        return validated;
    }

    private RuntimeConfigurationRevision<T> Validate(RuntimeConfigurationRevision<T> revision)
    {
        if (revision.Revision < 1 || revision.Revision < _highestRevision)
            throw new InvalidOperationException("The durable configuration revision regressed.");
        var value = _codec.Deserialize(_codec.Serialize(revision.Value));
        _highestRevision = revision.Revision;
        return revision with { Value = value };
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    [LoggerMessage(LogLevel.Warning, "Runtime configuration cache bypassed ({FailureType}); consulting the durable authority")]
    private static partial void LogCacheRejected(ILogger logger, string failureType);

    private sealed record CachedSnapshot
    {
        public required string Identity { get; init; }

        public required DateTimeOffset ValidUntilUtc { get; init; }

        public required RuntimeConfigurationRevision<T>? Revision { get; init; }
    }
}
