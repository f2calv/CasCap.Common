using CasCap.Common.Abstractions;
using CasCap.Common.Configuration.Redis.Extensions;
using CasCap.Common.Configuration.Redis.Models;
using CasCap.Common.Configuration.Redis.Services;
using CasCap.Common.Configuration.Sqlite;
using CasCap.Common.Configuration.Sqlite.Extensions;
using CasCap.Common.Configuration.Sqlite.Services;
using CasCap.Common.Extensions;
using CasCap.Common.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

public abstract class SqliteTestBase(ITestOutputHelper output) : IAsyncLifetime
{
    protected readonly ITestOutputHelper _output = output;
    protected readonly string _environment = $"arbitrary-{Guid.NewGuid():N}";
    private readonly string _directory = Path.Combine(
        Environment.GetEnvironmentVariable("SQLITETEST_DIRECTORY") ?? Path.Combine(Directory.GetCurrentDirectory(), ".copilot-tracking", "sqlite-tests"),
        Guid.NewGuid().ToString("N"));
    protected string _connectionString = null!;
    protected ServiceProvider _services = null!;
    protected SqliteRuntimeConfigurationStore<RuntimeControls> _store = null!;
    protected RuntimeConfigurationCacheOptions _cacheSettings = new()
    {
        AuthorityId = $"isolated-sqlite-{Guid.NewGuid():N}",
        MaximumAge = TimeSpan.FromSeconds(1),
        ReconciliationInterval = TimeSpan.FromSeconds(1),
    };

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_directory, "configuration.db"),
            Pooling = false,
            DefaultTimeout = 1,
        }.ConnectionString;
        await ExecuteAsync("CREATE TABLE application_sentinel (value TEXT NOT NULL); INSERT INTO application_sentinel VALUES ('retained');");
        _services = CreateServices();
        _store = _services.GetRequiredService<SqliteRuntimeConfigurationStore<RuntimeControls>>();
        await using var db = await CreateDbAsync();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        _output.WriteLine("Migrated an isolated SQLite file containing an unrelated application table.");
    }

    protected ServiceProvider CreateServices(
        string? environment = null,
        string application = "test-application",
        string contract = "controls",
        string? connectionString = null
    )
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders());
        services.AddSqliteRuntimeConfiguration(new RuntimeConfigurationOptions
        {
            Scope = new RuntimeConfigurationScope(application, environment ?? _environment),
            ContractId = contract,
            RefreshInterval = TimeSpan.FromMilliseconds(100),
        }, connectionString ?? _connectionString, new RuntimeControls());
        return services.BuildServiceProvider();
    }

    protected RuntimeConfigurationHostFactory CreateHost(bool cached = false, TimeProvider? clock = null) =>
        new(_connectionString, _environment, clock, cached ? AddCache : null, "Sqlite");

    protected void AddCache(IServiceCollection services)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddStandardConfiguration("Test").AddEnvironmentVariables("RuntimeConfigurationTests__");
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Configure the disposable test Redis connection.");
        var multiplexer = services.AddCasCapCaching(config =>
        {
            config.CacheExpiryServiceEnabled = false;
            config.RemoteCache = config.RemoteCache with { SerializationType = SerializationType.Json };
        }, connectionString);
        services.Replace(ServiceDescriptor.Singleton<IConnectionMultiplexer>(_ => multiplexer!));
        services.AddRuntimeConfigurationCache<RuntimeControls, SqliteRuntimeConfigurationStore<RuntimeControls>>(_cacheSettings);
    }

    protected static async Task DeleteCacheAsync(IServiceProvider services)
    {
        var reader = services.GetRequiredService<CachedRuntimeConfigurationStore<RuntimeControls>>();
        await services.GetRequiredService<IDistributedCache>().Delete(reader.CacheKey);
    }

    protected Task<RuntimeConfigurationDbContext> CreateDbAsync() =>
        _services.GetRequiredService<IDbContextFactory<RuntimeConfigurationDbContext>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

    protected Task<RuntimeConfigurationRevision<RuntimeControls>> PublishAsync(
        RuntimeControls value,
        long expectedRevision,
        DateTimeOffset? expiresAtUtc = null
    ) => _store.PublishAsync(value, expectedRevision, "test-operator", "Isolated publication",
        expiresAtUtc, TestContext.Current.CancellationToken);

    protected async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        if (!Directory.Exists(_directory))
            return;
        foreach (var file in Directory.EnumerateFiles(_directory))
            File.Delete(file);
        Directory.Delete(_directory);
    }
}
