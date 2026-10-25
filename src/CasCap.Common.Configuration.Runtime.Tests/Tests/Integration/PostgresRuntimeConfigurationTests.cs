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

    [Fact]
    public async Task ConcurrentPublication()
    {
        var publications = Enumerable.Range(0, 8).Select(async index =>
        {
            try
            {
                await PublishAsync(new RuntimeControls(), 0, $"Concurrent publication {index}");
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        });
        var results = await Task.WhenAll(publications);
        Assert.Single(results, value => value);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.Null(await _store.ReadRevisionAsync(2, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnvironmentIsolation()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 0);
        await using var other = CreateServices("another-arbitrary-environment");
        var otherStore = other.GetRequiredService<PostgresRuntimeConfigurationStore<RuntimeControls>>();
        Assert.Null(await otherStore.ReadAsync(TestContext.Current.CancellationToken));
        await otherStore.PublishAsync(new RuntimeControls { Verbosity = 1 }, 0, "test-operator", "Second environment",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
        Assert.Equal(1, (await otherStore.ReadAsync(TestContext.Current.CancellationToken))!.Value.Verbosity);
    }

    [Fact]
    public async Task MigrationsMatchModel()
    {
        await using var db = await _services.GetRequiredService<IDbContextFactory<RuntimeConfigurationDbContext>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.False(db.Database.HasPendingModelChanges());
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
    public async Task InvalidPublicationRetainsAcceptedSnapshot()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var host = CreateHost();
        using var client = host.CreateClient();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            BEGIN;
            INSERT INTO runtime_configuration.revisions
                (application, environment, contract_id, revision, contract_version, payload, recorded_at_utc, actor, reason)
            VALUES ('test-application', @environment, 'controls', 2, 1, '{"Verbosity":99}', now(), 'test-operator', 'Invalid candidate');
            UPDATE runtime_configuration.heads SET revision = 2 WHERE environment = @environment;
            COMMIT;
            """, connection);
        command.Parameters.AddWithValue("environment", _environment);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(3, (await client.GetFromJsonAsync<HostObservation>("/observe", TestContext.Current.CancellationToken))!.Controls.Verbosity);
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => _store.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExpiryRestoresBaseline()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 0);
        await using var host = CreateHost();
        using var client = host.CreateClient();
        Assert.Equal(5, (await client.GetFromJsonAsync<HostObservation>("/observe", TestContext.Current.CancellationToken))!.Controls.Verbosity);
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 1, expiresAtUtc: DateTimeOffset.UtcNow.AddSeconds(2));
        var expired = await WaitForAsync(client, value => value.Controls.Verbosity == 2);
        Assert.Equal(2, expired.Controls.Verbosity);
    }

    private static async Task<HostObservation> WaitForAsync(HttpClient client, Func<HostObservation, bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var value = (await client.GetFromJsonAsync<HostObservation>("/observe", timeout.Token))!;
            if (predicate(value))
                return value;
            await Task.Delay(50, timeout.Token);
        }
    }

    private Task<RuntimeConfigurationRevision<RuntimeControls>> PublishAsync(
        RuntimeControls value,
        long expectedRevision,
        string reason = "Test publication",
        DateTimeOffset? expiresAtUtc = null
    ) => _store.PublishAsync(value, expectedRevision, "test-operator", reason, expiresAtUtc, TestContext.Current.CancellationToken);
}
