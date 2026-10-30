using CasCap.Common.Configuration.PostgreSql;
using CasCap.Common.Configuration.PostgreSql.Extensions;
using CasCap.Common.Configuration.PostgreSql.Services;
using CasCap.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net.Http.Json;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

public abstract class TestBase(ITestOutputHelper output) : IAsyncLifetime
{
    protected readonly ITestOutputHelper _output = output;
    protected readonly string _environment = $"environment-{Guid.NewGuid():N}";
    private readonly string _database = $"configuration_test_{Guid.NewGuid():N}";
    private string _adminConnection = null!;
    protected string _connectionString = null!;
    protected string _redisConnectionString = null!;
    protected ServiceProvider _services = null!;
    protected PostgresRuntimeConfigurationStore<RuntimeControls> _store = null!;
    private bool _created;

    public async ValueTask InitializeAsync()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddStandardConfiguration("Test")
            .AddEnvironmentVariables("RuntimeConfigurationTests__");
        _adminConnection = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Postgres in appsettings.Test.json or RuntimeConfigurationTests__ConnectionStrings__Postgres for a disposable test server.");
        _redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Redis for a disposable test server.");
        var builder = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _database, Pooling = false };
        _connectionString = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _created = true;
        _services = CreateServices(_environment);
        _store = _services.GetRequiredService<PostgresRuntimeConfigurationStore<RuntimeControls>>();
        await using var db = await _services.GetRequiredService<IDbContextFactory<RuntimeConfigurationDbContext>>().CreateDbContextAsync();
        await db.Database.MigrateAsync();
        _output.WriteLine("Created and migrated an isolated test database.");
    }

    protected ServiceProvider CreateServices(string environment) => CreateServiceCollection(environment).BuildServiceProvider();

    protected ServiceCollection CreateServiceCollection(string environment)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddPostgresRuntimeConfiguration(
            new RuntimeConfigurationOptions
            {
                Scope = new RuntimeConfigurationScope("test-application", environment),
                ContractId = "controls",
                RefreshInterval = TimeSpan.FromMilliseconds(100),
            }, _connectionString, new RuntimeControls());
        return services;
    }

    protected RuntimeConfigurationHostFactory CreateHost(TimeProvider? timeProvider = null) =>
        new(_connectionString, _environment, timeProvider);

    protected async Task SetDatabaseAvailabilityAsync(bool available)
    {
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            $"ALTER DATABASE \"{_database}\" ALLOW_CONNECTIONS {(available ? "true" : "false")}", admin);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        if (!available)
        {
            await using var terminate = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database", admin);
            terminate.Parameters.AddWithValue("database", _database);
            await terminate.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    protected static async Task<HostObservation> WaitForAsync(HttpClient client, Func<HostObservation, bool> predicate)
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

    protected Task<RuntimeConfigurationRevision<RuntimeControls>> PublishAsync(
        RuntimeControls value,
        long expectedRevision,
        string reason = "Test publication",
        DateTimeOffset? expiresAtUtc = null
    ) => _store.PublishAsync(value, expectedRevision, "test-operator", reason, expiresAtUtc, TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        if (!_created)
            return;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }
}
