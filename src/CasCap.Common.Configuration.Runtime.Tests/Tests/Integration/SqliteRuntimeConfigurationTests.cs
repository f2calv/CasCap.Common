using CasCap.Common.Abstractions;
using CasCap.Common.Configuration.Redis.Services;
using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Sqlite;
using CasCap.Common.Configuration.Sqlite.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SqliteRuntimeConfigurationTests(ITestOutputHelper output) : SqliteTestBase(output)
{
    [Fact]
    public async Task Migration_DedicatedFileReopensPublishedState()
    {
        var connection = new SqliteConnectionStringBuilder(_connectionString);
        connection.DataSource = Path.ChangeExtension(connection.DataSource, "dedicated.db");
        await using var services = CreateServices(connectionString: connection.ConnectionString);
        await using (var db = await services.GetRequiredService<IDbContextFactory<RuntimeConfigurationDbContext>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        }
        var store = services.GetRequiredService<SqliteRuntimeConfigurationStore<RuntimeControls>>();
        await store.PublishAsync(new RuntimeControls { Verbosity = 4 }, 0, "test-operator", "Dedicated file",
            cancellationToken: TestContext.Current.CancellationToken);
        await using var reopened = CreateServices(connectionString: connection.ConnectionString);
        Assert.Equal(4, (await reopened.GetRequiredService<SqliteRuntimeConfigurationStore<RuntimeControls>>()
            .ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
        Assert.Null(await _store.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Migration_PreservesSharedFileAndMatchesModel()
    {
        await using var db = await CreateDbAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var tables = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains("cascap_config_heads", tables);
        Assert.Contains("cascap_config_revisions", tables);
        Assert.Contains("cascap_config_migrations_history", tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
        Assert.Equal("retained", await db.Database.SqlQueryRaw<string>("SELECT value AS Value FROM application_sentinel")
            .SingleAsync(TestContext.Current.CancellationToken));
        var sql = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE \"cascap_config_revisions\"", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("application_sentinel", sql);
    }

    [Fact]
    public async Task Publication_HistoryConflictAndReopen()
    {
        Assert.Null(await _store.ReadAsync(TestContext.Current.CancellationToken));
        var original = new RuntimeControls { Verbosity = 3, Categories = ["original"] };
        var first = await PublishAsync(original, 0);
        original.Categories[0] = "mutated";
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 1);
        Assert.Equal(["original"], first.Value.Categories);
        await using var reopened = CreateServices();
        var store = reopened.GetRequiredService<SqliteRuntimeConfigurationStore<RuntimeControls>>();
        var historical = (await store.ReadRevisionAsync(1, TestContext.Current.CancellationToken))!;
        Assert.Equal(3, historical.Value.Verbosity);
        Assert.Equal(["original"], historical.Value.Categories);
        Assert.Equal("test-operator", historical.Actor);
        Assert.Equal(4, (await store.ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => PublishAsync(new RuntimeControls(), 1));
        Assert.Null(await store.ReadRevisionAsync(3, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentPublication_OneWinner(bool existingHead)
    {
        if (existingHead)
            await PublishAsync(new RuntimeControls(), 0);
        var expected = existingHead ? 1 : 0;
        using var ready = new CountdownEvent(8);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            ready.Signal();
            await start.Task;
            try
            {
                await PublishAsync(new RuntimeControls { Verbosity = 4 }, expected);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }, TestContext.Current.CancellationToken)).ToArray();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        start.SetResult();
        Assert.Single(await Task.WhenAll(tasks), value => value);
        Assert.Equal(expected + 1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        await using var db = await CreateDbAsync();
        Assert.Equal(expected + 1, await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM cascap_config_revisions")
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusyOrLockedWriter_IsExplicitConflictAndDoesNotAppend(bool sharedReadLock)
    {
        await PublishAsync(new RuntimeControls(), 0);
        if (sharedReadLock)
        {
            _connectionString = new SqliteConnectionStringBuilder(_connectionString) { Cache = SqliteCacheMode.Shared }.ConnectionString;
            await _services.DisposeAsync();
            _services = CreateServices();
            _store = _services.GetRequiredService<SqliteRuntimeConfigurationStore<RuntimeControls>>();
        }
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = connection.BeginTransaction(deferred: sharedReadLock);
            if (sharedReadLock)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "SELECT revision FROM cascap_config_heads";
                await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            }
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Task.Run(
                () => PublishAsync(new RuntimeControls { Verbosity = 4 }, 1), TestContext.Current.CancellationToken));
        }
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.Null(await _store.ReadRevisionAsync(2, TestContext.Current.CancellationToken));
        Assert.Equal(2, (await PublishAsync(new RuntimeControls(), 1)).Revision);
    }

    [Fact]
    public async Task FailedHeadUpdate_RollsBackJournalAndPreservesProviderFailure()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await ExecuteAsync("""
            CREATE TRIGGER reject_test_head BEFORE UPDATE ON cascap_config_heads
            BEGIN SELECT RAISE(ABORT, 'Synthetic head rejection'); END;
            """);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => PublishAsync(new RuntimeControls(), 1));
        Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.Null(await _store.ReadRevisionAsync(2, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidDurableCandidate_IsRejected(bool unsupportedVersion)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await ExecuteAsync(unsupportedVersion
            ? "UPDATE cascap_config_revisions SET contract_version = 2"
            : """UPDATE cascap_config_revisions SET payload = '{"Verbosity":99}'""");
        if (unsupportedVersion)
            await Assert.ThrowsAsync<InvalidOperationException>(() => _store.ReadAsync(TestContext.Current.CancellationToken));
        else
            await Assert.ThrowsAsync<ValidationException>(() => _store.ReadRevisionAsync(1, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public async Task ScopeIsolation_ApplicationEnvironmentAndContract(int environments)
    {
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 0);
        foreach (var application in new[] { "other-application", "test-application" })
            foreach (var contract in new[] { "other-controls", "controls" })
                for (var index = 0; index < environments; index++)
                {
                    await using var services = CreateServices($"nonstandard/{index}", application, contract);
                    var store = services.GetRequiredService<SqliteRuntimeConfigurationStore<RuntimeControls>>();
                    Assert.Null(await store.ReadAsync(TestContext.Current.CancellationToken));
                    await store.PublishAsync(new RuntimeControls { Verbosity = index % 6 }, 0, "test-operator", "Scoped publication",
                        cancellationToken: TestContext.Current.CancellationToken);
                }
        foreach (var application in new[] { "other-application", "test-application" })
            foreach (var contract in new[] { "other-controls", "controls" })
                for (var index = 0; index < environments; index++)
                {
                    await using var services = CreateServices($"nonstandard/{index}", application, contract);
                    var store = services.GetRequiredService<SqliteRuntimeConfigurationStore<RuntimeControls>>();
                    Assert.Equal(index % 6, (await store.ReadRevisionAsync(1, TestContext.Current.CancellationToken))!.Value.Verbosity);
                }
        Assert.Equal(5, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
    }

    [Fact]
    public async Task InvalidPublication_DoesNotAppend()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await Assert.ThrowsAsync<ValidationException>(() => PublishAsync(new RuntimeControls { Verbosity = 99 }, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => PublishAsync(new RuntimeControls(), -1));
        await Assert.ThrowsAsync<ArgumentException>(() => PublishAsync(new RuntimeControls(), 1, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.Null(await _store.ReadRevisionAsync(2, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Host_RequiresInitialPublication(bool cached)
    {
        var builder = new HostBuilder().ConfigureServices(services =>
        {
            foreach (var descriptor in CreateServiceCollection())
                services.Add(descriptor);
            if (cached)
                AddCache(services);
        });
        using var host = builder.Build();
        // WebApplicationFactory can mask fast startup failures with its disposed deferred host.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Equal("A required runtime configuration revision has not been published.", exception.Message);
        if (cached)
        {
            Assert.Null(await _store.ReadAsync(TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Host_CompleteReplacementExpiryAndRetainedSentinel(bool cached)
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3, Categories = ["one", "two"] }, 0);
        var clock = new ManualTimeProvider();
        await using var host = CreateHost(cached, clock);
        using var client = host.CreateClient();
        try
        {
            var initial = await TestBase.WaitForAsync(client, value => value.Controls.Verbosity == 3);
            var monitor = host.Services.GetRequiredService<IOptionsMonitor<RuntimeControls>>();
            Assert.IsType<OptionsMonitor<RuntimeControls>>(monitor);
            var publisher = host.Services.GetRequiredService<IRuntimeConfigurationPublisher<RuntimeControls>>();
            Assert.IsType<SqliteRuntimeConfigurationStore<RuntimeControls>>(publisher);
            await publisher.PublishAsync(new RuntimeControls { Verbosity = 4, Categories = ["remaining"] }, 1,
                "test-operator", "Temporary override", clock.GetUtcNow().AddMinutes(10), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(2));
            var changed = await TestBase.WaitForAsync(client, value => value.Controls.Verbosity == 4);
            Assert.Equal(["remaining"], changed.Controls.Categories);
            Assert.Equal(initial.Sentinel, changed.Sentinel);
            clock.Advance(TimeSpan.FromMinutes(11));
            var expired = await TestBase.WaitForAsync(client, value => value.Controls.Verbosity == 2);
            Assert.Empty(expired.Controls.Categories);
            Assert.Equal(initial.Sentinel, expired.Sentinel);
            Assert.Equal(3, initial.Controls.Verbosity);
        }
        finally
        {
            if (cached)
                await DeleteCacheAsync(host.Services);
        }
    }

    [Fact]
    public async Task WarmCache_DoesNotReplaceAuthoritativeStartup()
    {
        _cacheSettings = _cacheSettings with
        {
            MaximumAge = TimeSpan.FromSeconds(30),
            ReconciliationInterval = TimeSpan.FromMinutes(1),
        };
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var warmer = CreateHost(cached: true);
        using var firstClient = warmer.CreateClient();
        await TestBase.WaitForAsync(firstClient, value => value.Controls.Verbosity == 3);
        var cached = warmer.Services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        await cached.ReadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await warmer.Services.GetRequiredService<IDistributedCache>().Get<string>(cached.CacheKey));
        try
        {
            // Keep the valid cached envelope but remove this test's durable head.
            await ExecuteAsync("DELETE FROM cascap_config_heads;");
            await using var replacement = CreateHost(cached: true);
            var exception = Assert.Throws<InvalidOperationException>(() => replacement.CreateClient());
            Assert.Equal("A required runtime configuration revision has not been published.", exception.Message);
            Assert.NotNull(await warmer.Services.GetRequiredService<IDistributedCache>().Get<string>(cached.CacheKey));
        }
        finally
        {
            await DeleteCacheAsync(warmer.Services);
        }
    }
}
