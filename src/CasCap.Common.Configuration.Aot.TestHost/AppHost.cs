using CasCap.Common.Abstractions;
using CasCap.Common.Configuration.Aot.TestHost.Models;
using CasCap.Common.Configuration.Aot.TestHost.Services;
using CasCap.Common.Extensions;
using CasCap.Common.Models;
using CasCap.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PgContext = CasCap.Common.Configuration.PostgreSql.RuntimeConfigurationDbContext;
using SqliteContext = CasCap.Common.Configuration.Sqlite.RuntimeConfigurationDbContext;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CasCap.Common.Configuration.Aot.TestHost;

/// <summary>Runs identical persistence and monitor scenarios in published managed and native processes.</summary>
internal static class AppHost
{
    /// <summary>Runs one bounded phase; any contract failure terminates the child process unsuccessfully.</summary>
    public static async Task RunAsync(string[] args)
    {
        if (args.Length != 3 || args[0] is not ("postgres" or "sqlite")
            || args[1] is not ("jit" or "aot")
            || args[2] is not ("migrate" or "exercise" or "verify" or "dynamic-filter" or "dynamic-aggregate"))
            throw new ArgumentException("Expected: <postgres|sqlite> <jit|aot> <migrate|exercise|verify|dynamic-filter|dynamic-aggregate>.");
        Ensure(RuntimeFeature.IsDynamicCodeSupported == (args[1] == "jit"), "Actual executable deployment mode");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var queryProbe = args[2] is "dynamic-filter" or "dynamic-aggregate";
        using var host = CreateHost(args[0], queryProbe ? "jit" : args[1]);
        var services = host.Services;
        if (queryProbe)
        {
            await DynamicQueryProbe.RunAsync(services, args[0], args[1], args[2], timeout.Token);
            return;
        }
        var publisher = services.GetRequiredService<IRuntimeConfigurationPublisher<Controls>>();
        if (args[2] == "migrate")
        {
#if NATIVEAOT
            throw new NotSupportedException("Migrations must run in a managed process.");
#else
            if (args[0] == "postgres")
            {
                await using var db = await services.GetRequiredService<IDbContextFactory<PgContext>>().CreateDbContextAsync(timeout.Token);
                await db.Database.MigrateAsync(timeout.Token);
            }
            else
            {
                await using var db = await services.GetRequiredService<IDbContextFactory<SqliteContext>>().CreateDbContextAsync(timeout.Token);
                await db.Database.MigrateAsync(timeout.Token);
            }
            return;
#endif
        }
        if (args[2] == "verify")
        {
            var value = await publisher.ReadAsync(timeout.Token);
            Ensure(value is { Revision: 5, Value.Enabled: true }, "Persistence across executable restarts");
            Ensure((await ReadRevisionAsync(services, 1, timeout.Token))?.Value.Children.Length == 1, "Immutable history after reopen");
            return;
        }
        await ExerciseAsync(host, publisher, args, timeout.Token);
    }

    private static IHost CreateHost(string backend, string environment)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
        });
        builder.Configuration.AddEnvironmentVariables("ConfigurationAot__");
        builder.Logging.ClearProviders();
        var connection = builder.Configuration["Connection"]
            ?? throw new InvalidOperationException("Set ConfigurationAot__Connection to an isolated test database.");
        var settings = new RuntimeConfigurationOptions
        {
            Scope = new RuntimeConfigurationScope("published-test", environment),
            ContractId = "controls",
            RefreshInterval = TimeSpan.FromMilliseconds(100),
        };
        if (backend == "postgres")
            builder.Services.AddPostgresRuntimeConfiguration(settings, connection, new Controls(),
                ControlsJsonContext.Default.Controls, new ControlsValidator());
        else
            builder.Services.AddSqliteRuntimeConfiguration(settings, connection, new Controls(),
                ControlsJsonContext.Default.Controls, new ControlsValidator());
        return builder.Build();
    }

    private static async Task ExerciseAsync(
        IHost host,
        IRuntimeConfigurationPublisher<Controls> publisher,
        string[] args,
        CancellationToken token
    )
    {
        Ensure(await publisher.ReadAsync(token) is null, "Initially missing head");
        var first = await publisher.PublishAsync(new Controls { Enabled = true, Children = [new() { Count = 2 }] },
            0, "test-operator", "First", cancellationToken: token);
        Ensure(first.Revision == 1, "First publication");
        await host.StartAsync(token);
        try
        {
            var monitor = host.Services.GetRequiredService<IOptionsMonitor<Controls>>();
            Ensure(monitor.CurrentValue.Enabled && monitor.CurrentValue.Children.Length == 1, "Startup monitor");
            var changes = 0;
            using var registration = monitor.OnChange(_ => Interlocked.Increment(ref changes));
            await publisher.PublishAsync(new Controls(), 1, "test-operator", "Second", cancellationToken: token);
            await WaitForAsync(() => !monitor.CurrentValue.Enabled && monitor.CurrentValue.Children.Length == 0
                && Volatile.Read(ref changes) > 0, token);
            Ensure((await ReadRevisionAsync(host.Services, 1, token))?.Value.Children.Length == 1, "Historical snapshot");
            await ExpectAsync<DbUpdateConcurrencyException>(() =>
                publisher.PublishAsync(new Controls(), 1, "test-operator", "Stale", cancellationToken: token));
            Ensure(await ReadRevisionAsync(host.Services, 3, token) is null, "No phantom history after stale write");
            await AssertValidationAsync(host.Services, publisher, token);
            using var other = CreateHost(args[0], args[1] + "-isolated");
            var isolatedPublisher = other.Services.GetRequiredService<IRuntimeConfigurationPublisher<Controls>>();
            Ensure(await isolatedPublisher.ReadAsync(token) is null, "Environment isolation");
            var firstWriters = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => TryPublishAsync(isolatedPublisher, 0, token)));
            Ensure(firstWriters.Count(success => success) == 1, "Exactly one concurrent first publisher");
            var writers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => TryPublishAsync(publisher, 2, token)));
            Ensure(writers.Count(success => success) == 1, "Exactly one concurrent writer");
            Ensure((await publisher.ReadAsync(token))?.Revision == 3, "Concurrent revision");
            await publisher.PublishAsync(new Controls { Enabled = true }, 3, "test-operator", "Expiring",
                DateTimeOffset.UtcNow.AddSeconds(3), token);
            await WaitForAsync(() => monitor.CurrentValue.Enabled, token);
            await WaitForAsync(() => !monitor.CurrentValue.Enabled, token);
            await publisher.PublishAsync(new Controls { Enabled = true }, 4, "test-operator", "Durable", cancellationToken: token);
            await WaitForAsync(() => monitor.CurrentValue.Enabled, token);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task AssertValidationAsync(
        IServiceProvider services,
        IRuntimeConfigurationPublisher<Controls> publisher,
        CancellationToken token
    )
    {
        var codec = services.GetRequiredService<RuntimeConfigurationCodec<Controls>>();
        foreach (var json in new[] { """{"Unknown":true}""", """{"Children":[{"Unknown":true}]}""", """{"Enabled":"invalid"}""" })
            await ExpectAsync<JsonException>(() => Task.FromResult(codec.Deserialize(json)));
        foreach (var value in new Controls[]
        {
            new() { Day = (DayOfWeek)99 },
            new() { Minimum = 5, Maximum = 1 },
            new() { Children = [new() { Count = -1 }] },
            new() { Children = [new() { Label = new string('a', 65_536) }] },
        })
            await ExpectAsync<ValidationException>(() =>
                publisher.PublishAsync(value, 2, "test-operator", "Invalid", cancellationToken: token));
        Ensure((await publisher.ReadAsync(token))?.Revision == 2, "Invalid values never advance head");
        Ensure(await ReadRevisionAsync(services, 3, token) is null, "Invalid values never append history");
    }

    private static Task<RuntimeConfigurationRevision<Controls>?> ReadRevisionAsync(IServiceProvider services, long revision, CancellationToken token) =>
        services.GetService<PostgresRuntimeConfigurationStore<Controls>>() is { } postgres
            ? postgres.ReadRevisionAsync(revision, token)
            : services.GetRequiredService<SqliteRuntimeConfigurationStore<Controls>>().ReadRevisionAsync(revision, token);

    private static async Task<bool> TryPublishAsync(IRuntimeConfigurationPublisher<Controls> publisher, long expectedRevision, CancellationToken token)
    {
        try
        {
            await publisher.PublishAsync(new Controls(), expectedRevision, "test-operator", "Concurrent", cancellationToken: token);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static async Task ExpectAsync<TException>(Func<Task> operation) where TException : Exception
    {
        try
        {
            await operation();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException("Expected contract rejection: " + typeof(TException).Name);
    }

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition())
            await Task.Delay(25, token);
    }

    private static void Ensure(bool condition, string scenario)
    {
        if (!condition)
            throw new InvalidOperationException("Published configuration check failed: " + scenario);
    }
}
