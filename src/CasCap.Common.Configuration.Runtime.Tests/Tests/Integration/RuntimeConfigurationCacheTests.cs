using CasCap.Common.Abstractions;
using CasCap.Common.Configuration.PostgreSql.Services;
using CasCap.Common.Configuration.Redis.Extensions;
using CasCap.Common.Configuration.Redis.Models;
using CasCap.Common.Configuration.Redis.Services;
using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using System.Text.Json.Nodes;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class RuntimeConfigurationCacheTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task SharedRedisHit_ReducesReadsAndReconcilesUninvalidatedPublication()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        var clock = new ManualTimeProvider();
        var settings = CacheSettings();
        await using var first = CreateCachedServices(settings, clock);
        await using var second = CreateCachedServices(settings, clock);
        var firstReader = first.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var secondReader = second.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var source = first.GetRequiredService<CountingStore>();
        var secondSource = second.GetRequiredService<CountingStore>();
        var cache = first.GetRequiredService<IDistributedCache>();
        try
        {
            Assert.Equal(3, (await ReadAsync(firstReader))!.Value.Verbosity);
            Assert.Equal(3, (await ReadAsync(secondReader))!.Value.Verbosity);
            await ReadAsync(firstReader);
            Assert.Equal(2, source.ReadCount);
            await ReadAsync(secondReader);
            Assert.Equal(1, secondSource.ReadCount);
            Assert.NotNull(await cache.Get<string>(firstReader.CacheKey));
            for (var index = 0; index < 20; index++)
                Assert.Equal(3, (await ReadAsync(firstReader))!.Value.Verbosity);
            Assert.Equal(2, source.ReadCount);

            await PublishDirectSqlAsync();
            Assert.Equal(3, (await ReadAsync(firstReader))!.Value.Verbosity);
            clock.Advance(settings.ReconciliationInterval);
            Assert.Equal(4, (await ReadAsync(firstReader))!.Value.Verbosity);
            Assert.Equal(4, (await ReadAsync(secondReader))!.Value.Verbosity);
            Assert.Equal(3, source.ReadCount);
            Assert.Equal(2, secondSource.ReadCount);
            // An unexpired stale refill cannot regress a reader that already saw revision 2.
            Assert.Equal(4, (await ReadAsync(firstReader))!.Value.Verbosity);
        }
        finally
        {
            await cache.Delete(firstReader.CacheKey, CommandFlags.None);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CacheFailure_FallsBackToAuthority(bool corruptEntry)
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var services = CreateCachedServices(CacheSettings(), TimeProvider.System);
        var reader = services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var cache = services.GetRequiredService<IDistributedCache>();
        try
        {
            await ReadAsync(reader);
            if (corruptEntry)
                await cache.Set(reader.CacheKey, "{malformed", absoluteExpiration: DateTimeOffset.UtcNow.AddSeconds(30));
            else
                services.GetRequiredService<FaultingCache>().Unavailable = true;
            await PublishAsync(new RuntimeControls { Verbosity = 4 }, 1);
            Assert.Equal(4, (await ReadAsync(reader))!.Value.Verbosity);
        }
        finally
        {
            services.GetRequiredService<FaultingCache>().Unavailable = false;
            await cache.Delete(reader.CacheKey, CommandFlags.None);
        }
    }

    [Fact]
    public async Task CacheAge_IsAbsoluteAndMissEvictionRefills()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        var clock = new ManualTimeProvider();
        var settings = CacheSettings();
        await using var services = CreateCachedServices(settings, clock);
        var reader = services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var cache = services.GetRequiredService<IDistributedCache>();
        try
        {
            await ReadAsync(reader);
            await ReadAsync(reader);
            await PublishAsync(new RuntimeControls { Verbosity = 4 }, 1);
            clock.Advance(settings.MaximumAge);
            Assert.Equal(4, (await ReadAsync(reader))!.Value.Verbosity);
            await cache.Delete(reader.CacheKey, CommandFlags.None);
            await ReadAsync(reader);
            await PublishAsync(new RuntimeControls { Verbosity = 5 }, 2);
            await cache.Delete(reader.CacheKey, CommandFlags.None);
            Assert.Equal(5, (await ReadAsync(reader))!.Value.Verbosity);
        }
        finally
        {
            await cache.Delete(reader.CacheKey, CommandFlags.None);
        }
    }

    [Fact]
    public async Task Startup_RequiresAuthorityEvenWithWarmCache()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        var settings = CacheSettings();
        await using var services = CreateCachedServices(settings, TimeProvider.System);
        var reader = services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var cache = services.GetRequiredService<IDistributedCache>();
        try
        {
            await ReadAsync(reader);
            await ReadAsync(reader);
            Assert.NotNull(await cache.Get<string>(reader.CacheKey));
            await using var replacement = CreateCachedServices(settings, TimeProvider.System);
            await SetDatabaseAvailabilityAsync(false);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ReadAsync(replacement.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>()));
            Assert.Equal("55000", Assert.IsType<Npgsql.PostgresException>(failure.InnerException).SqlState);
        }
        finally
        {
            await SetDatabaseAvailabilityAsync(true);
            await cache.Delete(reader.CacheKey, CommandFlags.None);
        }
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("validation")]
    [InlineData("regression")]
    public async Task RejectedCacheEntry_UsesAuthority(string fault)
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        var clock = new ManualTimeProvider();
        var settings = CacheSettings();
        await using var services = CreateCachedServices(settings, clock);
        var reader = services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var cache = services.GetRequiredService<IDistributedCache>();
        try
        {
            await ReadAsync(reader);
            await ReadAsync(reader);
            var stale = JsonNode.Parse((await cache.Get<string>(reader.CacheKey))!)!;
            await PublishAsync(new RuntimeControls { Verbosity = 4 }, 1);
            clock.Advance(settings.ReconciliationInterval);
            Assert.Equal(4, (await ReadAsync(reader))!.Value.Verbosity);
            stale["ValidUntilUtc"] = clock.GetUtcNow().Add(settings.MaximumAge);
            if (fault == "scope")
                stale["Identity"] = "different-authority";
            else if (fault == "validation")
            {
                stale["Revision"]!["Revision"] = 2;
                stale["Revision"]!["Value"]!["Verbosity"] = 99;
            }
            await cache.Set(reader.CacheKey, stale.ToJsonString(), absoluteExpiration: DateTimeOffset.UtcNow.AddMinutes(2));
            Assert.Equal(4, (await ReadAsync(reader))!.Value.Verbosity);
        }
        finally
        {
            await cache.Delete(reader.CacheKey, CommandFlags.None);
        }
    }

    [Fact]
    public async Task CachedHost_ReconcilesPublicationWithoutRestart()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        var clock = new ManualTimeProvider();
        var settings = CacheSettings();
        await using var host = new RuntimeConfigurationHostFactory(_connectionString, _environment, clock,
            services =>
            {
                AddCaching(services);
                services.AddRuntimeConfigurationCache<RuntimeControls, PostgresRuntimeConfigurationStore<RuntimeControls>>(settings);
            });
        using var client = host.CreateClient();
        var initial = await WaitForAsync(client, value => value.Controls.Verbosity == 3);
        var reader = host.Services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var cache = host.Services.GetRequiredService<IDistributedCache>();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (await cache.Get<string>(reader.CacheKey) is null)
                await Task.Delay(50, timeout.Token);
            await PublishAsync(new RuntimeControls { Verbosity = 5 }, 1);
            clock.Advance(settings.ReconciliationInterval);
            var changed = await WaitForAsync(client, value => value.Controls.Verbosity == 5);
            Assert.Equal(initial.Sentinel, changed.Sentinel);
        }
        finally
        {
            await host.Services.GetRequiredService<IHost>().StopAsync(TestContext.Current.CancellationToken);
            await using var cleanup = CreateCachedServices(settings, clock);
            await cleanup.GetRequiredService<IDistributedCache>().Delete(reader.CacheKey, CommandFlags.None);
        }
    }

    [Fact]
    public void Keys_IsolateAuthorityApplicationEnvironmentAndContract()
    {
        var settings = CacheSettings();
        var contract = _services.GetRequiredService<RuntimeConfigurationOptions>();
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            Key(settings, contract),
            Key(settings with { AuthorityId = "other-authority" }, contract),
            Key(settings, contract with { Scope = new RuntimeConfigurationScope("other-application", _environment) }),
            Key(settings, contract with { Scope = new RuntimeConfigurationScope(contract.Scope.Application, "other-environment") }),
            Key(settings, contract with { ContractId = "other-contract" }),
            Key(settings, contract with { ContractVersion = 2 }),
        };
        Assert.Equal(6, keys.Count);
        Assert.All(keys, key => Assert.Matches("^configuration:cache:v1:[0-9a-f]{64}$", key));
    }

    private string Key(RuntimeConfigurationCacheOptions settings, RuntimeConfigurationOptions contract)
    {
        using var store = new CachedRuntimeConfigurationStore<RuntimeControls>(
            NullLogger<CachedRuntimeConfigurationStore<RuntimeControls>>.Instance, TimeProvider.System,
            settings, contract, new FaultingCache(null), _store,
            new CasCap.Common.Configuration.Runtime.Services.RuntimeConfigurationCodec<RuntimeControls>([]));
        return store.CacheKey;
    }

    [Fact]
    public async Task SlowCacheFill_UsesAuthorityWhenPhysicalDeadlineElapses()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        var settings = CacheSettings() with { MaximumAge = TimeSpan.FromMilliseconds(100) };
        await using var services = CreateCachedServices(settings, TimeProvider.System);
        var reader = services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        var source = services.GetRequiredService<CountingStore>();
        await ReadAsync(reader);
        source.Delay = TimeSpan.FromMilliseconds(250);
        try
        {
            var revision = await ReadAsync(reader);
            Assert.Equal(3, revision!.Value.Verbosity);
            Assert.Equal(3, source.ReadCount);
        }
        finally
        {
            await services.GetRequiredService<IDistributedCache>().Delete(reader.CacheKey);
        }
    }

    private ServiceProvider CreateCachedServices(RuntimeConfigurationCacheOptions settings, TimeProvider clock)
    {
        var services = CreateServiceCollection(_environment);
        services.AddSingleton(clock);
        AddCaching(services);
        var descriptor = services.Single(value => value.ServiceType == typeof(IDistributedCache));
        services.Remove(descriptor);
        services.AddSingleton(provider => new FaultingCache(
            ActivatorUtilities.CreateInstance<CasCap.Common.Services.DistributedCacheService>(provider)));
        services.AddSingleton<IDistributedCache>(provider => provider.GetRequiredService<FaultingCache>());
        services.AddSingleton<CountingStore>();
        services.AddRuntimeConfigurationCache<RuntimeControls, CountingStore>(settings);
        return services.BuildServiceProvider();
    }

    private void AddCaching(IServiceCollection services)
    {
        var multiplexer = services.AddCasCapCaching(config =>
        {
            config.CacheExpiryServiceEnabled = false;
            config.RemoteCache = config.RemoteCache with { SerializationType = SerializationType.Json };
        }, _redisConnectionString);
        // The helper registers an external instance; the fixture transfers disposal ownership to DI.
        services.Replace(ServiceDescriptor.Singleton<IConnectionMultiplexer>(_ => multiplexer!));
    }

    private static RuntimeConfigurationCacheOptions CacheSettings() => new()
    {
        AuthorityId = $"isolated-test-{Guid.NewGuid():N}",
        MaximumAge = TimeSpan.FromSeconds(30),
        ReconciliationInterval = TimeSpan.FromMinutes(1),
    };

    private async Task PublishDirectSqlAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new Npgsql.NpgsqlCommand("""
            BEGIN;
            INSERT INTO cascap_config.revisions
                (application, environment, contract_id, revision, contract_version, payload, recorded_at_utc, actor, reason)
            VALUES ('test-application', @environment, 'controls', 2, 1, '{"Verbosity":4}', now(), 'test-operator', 'Direct SQL cache reconciliation');
            UPDATE cascap_config.heads SET revision = 2
            WHERE application = 'test-application' AND environment = @environment AND contract_id = 'controls' AND revision = 1;
            COMMIT;
            """, connection);
        command.Parameters.AddWithValue("environment", _environment);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static Task<RuntimeConfigurationRevision<RuntimeControls>?> ReadAsync(CachedRuntimeConfigurationStore<RuntimeControls> reader) =>
        reader.ReadAsync(TestContext.Current.CancellationToken);

    private sealed class CountingStore(PostgresRuntimeConfigurationStore<RuntimeControls> authority) : IRuntimeConfigurationStore<RuntimeControls>
    {
        public int ReadCount;
        public TimeSpan Delay { get; set; }

        public async Task<RuntimeConfigurationRevision<RuntimeControls>?> ReadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ReadCount);
            await Task.Delay(Delay, cancellationToken);
            return await authority.ReadAsync(cancellationToken);
        }
    }

    private sealed class FaultingCache(IDistributedCache? inner) : IDistributedCache
    {
        public bool Unavailable { get; set; }

        public event EventHandler<PostEvictionEventArgs> PostEvictionEvent
        {
            add { if (inner is not null) inner.PostEvictionEvent += value; }
            remove { if (inner is not null) inner.PostEvictionEvent -= value; }
        }

        public Task<T?> Get<T>(string key) where T : class => Get<T>(key, null);

        public Task<T?> Get<T>(string key, Func<Task<T>>? createItem = null, TimeSpan? slidingExpiration = null,
            DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) where T : class =>
            Unavailable
                ? throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "Synthetic cache outage.")
                : inner!.Get(key, createItem, slidingExpiration, absoluteExpiration, flags);

        public Task Set<T>(string key, T cacheEntry) where T : class => inner!.Set(key, cacheEntry);

        public Task Set<T>(string key, T cacheEntry, TimeSpan? slidingExpiration = null,
            DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) where T : class =>
            inner!.Set(key, cacheEntry, slidingExpiration, absoluteExpiration, flags);

        public Task<bool> Delete(string key, CommandFlags flags = CommandFlags.None) => inner!.Delete(key, flags);

        public Task<long> DeleteAll(CommandFlags flags = CommandFlags.None, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Tests must never flush shared Redis.");
    }
}
