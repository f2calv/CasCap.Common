using CasCap.Common.Configuration.PostgreSql;
using CasCap.Common.Configuration.PostgreSql.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Net.Http.Json;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class PostgresRuntimeConfigurationTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task Publication_HistoryAndConflict()
    {
        Assert.Null(await _store.ReadAsync(TestContext.Current.CancellationToken));
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 1);
        Assert.Equal(3, (await _store.ReadRevisionAsync(1, TestContext.Current.CancellationToken))!.Value.Verbosity);
        Assert.Equal(4, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            PublishAsync(new RuntimeControls(), 1));
        Assert.Null(await _store.ReadRevisionAsync(3, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentPublication(bool existingHead)
    {
        if (existingHead)
            await PublishAsync(new RuntimeControls(), 0);
        var expectedRevision = existingHead ? 1 : 0;
        var publications = Enumerable.Range(0, 8).Select(async index =>
        {
            try
            {
                await PublishAsync(new RuntimeControls(), expectedRevision, $"Concurrent publication {index}");
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        });
        var results = await Task.WhenAll(publications);
        Assert.Single(results, value => value);
        Assert.Equal(expectedRevision + 1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.Null(await _store.ReadRevisionAsync(expectedRevision + 2, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public async Task EnvironmentIsolation(int environmentCount)
    {
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 0);
        for (var index = 0; index < environmentCount; index++)
        {
            await using var other = CreateServices($"arbitrary-environment-{index}");
            var otherStore = other.GetRequiredService<PostgresRuntimeConfigurationStore<RuntimeControls>>();
            Assert.Null(await otherStore.ReadAsync(TestContext.Current.CancellationToken));
            await otherStore.PublishAsync(new RuntimeControls { Verbosity = index % 6 }, 0, "test-operator", "Isolated environment",
                cancellationToken: TestContext.Current.CancellationToken);
        }
        for (var index = 0; index < environmentCount; index++)
        {
            await using var other = CreateServices($"arbitrary-environment-{index}");
            var otherStore = other.GetRequiredService<PostgresRuntimeConfigurationStore<RuntimeControls>>();
            Assert.Equal(index % 6, (await otherStore.ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
        }
        Assert.Equal(5, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
    }

    [Fact]
    public async Task InvalidTypedPublication_DoesNotAppendHistory()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            PublishAsync(new RuntimeControls { Verbosity = 99 }, 1));
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.Null(await _store.ReadRevisionAsync(2, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MigrationsMatchModel()
    {
        await using var db = await _services.GetRequiredService<IDbContextFactory<RuntimeConfigurationDbContext>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("cascap_config", db.Model.GetDefaultSchema());
        var tables = await db.Database.SqlQuery<string>(
            $"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'cascap_config'")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["__ef_migrations_history", "heads", "revisions"], tables.Order(StringComparer.Ordinal).ToArray());
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingInitialPublicationFailsStartup()
    {
        await using var host = CreateHost();
        var exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Equal("A required runtime configuration revision has not been published.", exception.Message);
        Assert.Null(await _store.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MonitorReload_PreservesHostAndRemovesDeletedKeys()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3, Categories = ["one", "two"] }, 0);
        await using var host = CreateHost();
        using var client = host.CreateClient();
        var initial = (await client.GetFromJsonAsync<HostObservation>("/observe", TestContext.Current.CancellationToken))!;
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<RuntimeControls>>();
        Assert.IsType<OptionsMonitor<RuntimeControls>>(monitor);
        var notifications = 0;
        using var subscription = monitor.OnChange(_ => Interlocked.Increment(ref notifications));
        await PublishAsync(new RuntimeControls { Verbosity = 4, Categories = ["remaining"] }, 1);
        var changed = await WaitForAsync(client, value => value.Controls.Verbosity == 4);
        Assert.Equal(initial.Sentinel, changed.Sentinel);
        Assert.Equal(["remaining"], changed.Controls.Categories);
        await PublishAsync(new RuntimeControls { Verbosity = 4, Categories = ["remaining"] }, 2);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref notifications));
    }

    [Fact]
    public async Task InvalidPublicationRetainsAcceptedSnapshotUntilExpiry()
    {
        var timeProvider = new ManualTimeProvider();
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var host = CreateHost(timeProvider);
        using var client = host.CreateClient();
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 1, expiresAtUtc: timeProvider.GetUtcNow().AddMinutes(10));
        await WaitForAsync(client, value => value.Controls.Verbosity == 4);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            BEGIN;
            INSERT INTO cascap_config.revisions
                (application, environment, contract_id, revision, contract_version, payload, recorded_at_utc, actor, reason)
            VALUES ('test-application', @environment, 'controls', 3, 1, '{"Verbosity":99}', now(), 'test-operator', 'Invalid candidate');
            UPDATE cascap_config.heads SET revision = 3 WHERE environment = @environment;
            COMMIT;
            """, connection);
        command.Parameters.AddWithValue("environment", _environment);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(4, (await client.GetFromJsonAsync<HostObservation>("/observe", TestContext.Current.CancellationToken))!.Controls.Verbosity);
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => _store.ReadAsync(TestContext.Current.CancellationToken));
        timeProvider.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(2, (await WaitForAsync(client, value => value.Controls.Verbosity == 2)).Controls.Verbosity);
    }

    [Fact]
    public async Task ExpiryRestoresBaseline()
    {
        var timeProvider = new ManualTimeProvider();
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var host = CreateHost(timeProvider);
        using var client = host.CreateClient();
        Assert.Equal(3, (await client.GetFromJsonAsync<HostObservation>("/observe", TestContext.Current.CancellationToken))!.Controls.Verbosity);
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 1, expiresAtUtc: timeProvider.GetUtcNow().AddMinutes(10));
        await WaitForAsync(client, value => value.Controls.Verbosity == 5);
        timeProvider.Advance(TimeSpan.FromMinutes(20));
        var expired = await WaitForAsync(client, value => value.Controls.Verbosity == 2);
        Assert.Equal(2, expired.Controls.Verbosity);
    }

}
