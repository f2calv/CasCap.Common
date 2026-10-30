using CasCap.Common.Configuration.Runtime.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Net.Http.Json;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class RuntimeConfigurationResilienceTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task Replicas_ConvergeAndReplacementLoadsCurrentRevision()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var first = CreateHost();
        await using var second = CreateHost();
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        var firstInitial = await WaitForAsync(firstClient, value => value.Controls.Verbosity == 3);
        var secondInitial = await WaitForAsync(secondClient, value => value.Controls.Verbosity == 3);
        Assert.NotEqual(firstInitial.Sentinel, secondInitial.Sentinel);

        await PublishAsync(new RuntimeControls { Verbosity = 5, Categories = ["current"] }, 1);
        var observations = await Task.WhenAll(
            WaitForAsync(firstClient, value => value.Controls.Verbosity == 5),
            WaitForAsync(secondClient, value => value.Controls.Verbosity == 5));
        Assert.Equal(firstInitial.Sentinel, observations[0].Sentinel);
        Assert.Equal(secondInitial.Sentinel, observations[1].Sentinel);
        Assert.All(observations, value => Assert.Equal(["current"], value.Controls.Categories));

        await second.DisposeAsync();
        await using var replacement = CreateHost();
        using var replacementClient = replacement.CreateClient();
        var replaced = await WaitForAsync(replacementClient, value => value.Controls.Verbosity == 5);
        Assert.NotEqual(secondInitial.Sentinel, replaced.Sentinel);
        Assert.Equal(["current"], replaced.Controls.Categories);
        Assert.Equal(firstInitial.Sentinel, (await WaitForAsync(firstClient, value => value.Controls.Verbosity == 5)).Sentinel);
    }

    [Fact]
    public async Task DatabaseOutage_RetainsSnapshotExpiresAndRecovers()
    {
        var clock = new ManualTimeProvider();
        var failures = new RuntimeConfigurationFailureLog();
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await using var host = new RuntimeConfigurationHostFactory(_connectionString, _environment, clock,
            services => services.AddLogging(logging => logging.AddProvider(failures)));
        using var client = host.CreateClient();
        var initial = await WaitForAsync(client, value => value.Controls.Verbosity == 3);
        await PublishAsync(new RuntimeControls { Verbosity = 5 }, 1, expiresAtUtc: clock.GetUtcNow().AddMinutes(10));
        await WaitForAsync(client, value => value.Controls.Verbosity == 5);
        try
        {
            await SetDatabaseAvailabilityAsync(false);
            await failures.Failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            var retained = (await client.GetFromJsonAsync<HostObservation>("/observe", TestContext.Current.CancellationToken))!;
            Assert.Equal(5, retained.Controls.Verbosity);
            Assert.Equal(initial.Sentinel, retained.Sentinel);
            Assert.All(failures.Messages, message =>
            {
                Assert.DoesNotContain(_connectionString, message, StringComparison.Ordinal);
                Assert.DoesNotContain("Username", message, StringComparison.OrdinalIgnoreCase);
            });
            clock.Advance(TimeSpan.FromMinutes(20));
            Assert.Equal(initial.Sentinel, (await WaitForAsync(client, value => value.Controls.Verbosity == 2)).Sentinel);
        }
        finally
        {
            await SetDatabaseAvailabilityAsync(true);
        }
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 2);
        var recovered = await WaitForAsync(client, value => value.Controls.Verbosity == 4);
        Assert.Equal(initial.Sentinel, recovered.Sentinel);
    }

    [Fact]
    public async Task DatabaseUnavailable_FailsInitialLoad()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await SetDatabaseAvailabilityAsync(false);
        try
        {
            await using var host = CreateHost();
            var failure = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
            Assert.Equal("55000", Assert.IsType<PostgresException>(failure.InnerException).SqlState);
        }
        finally
        {
            await SetDatabaseAvailabilityAsync(true);
        }
    }

    [Fact]
    public async Task DurableRevisionRegression_RetainsAcceptedState()
    {
        var failures = new RuntimeConfigurationFailureLog();
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 0);
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 1);
        await using var host = new RuntimeConfigurationHostFactory(_connectionString, _environment,
            configureServices: services => services.AddLogging(logging => logging.AddProvider(failures)));
        using var client = host.CreateClient();
        var accepted = await WaitForAsync(client, value => value.Controls.Verbosity == 4);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE cascap_config.heads SET revision = 1
            WHERE application = 'test-application' AND environment = @environment AND contract_id = 'controls'
            """, connection);
        command.Parameters.AddWithValue("environment", _environment);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await failures.Failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var retained = (await client.GetFromJsonAsync<HostObservation>("/observe", TestContext.Current.CancellationToken))!;
        Assert.Equal(4, retained.Controls.Verbosity);
        Assert.Equal(accepted.Sentinel, retained.Sentinel);
    }

    [Fact]
    public async Task Reads_DoNotAccessStoreAndBlockedPollCancelsWithoutOverlap()
    {
        await PublishAsync(new RuntimeControls { Verbosity = 4 }, 0);
        BlockingStore? probe = null;
        await using var host = new RuntimeConfigurationHostFactory(_connectionString, _environment,
            configureServices: services => services.Replace(ServiceDescriptor.Singleton<IRuntimeConfigurationStore<RuntimeControls>>(provider =>
                probe = new BlockingStore(provider.GetRequiredService<CasCap.Common.Configuration.PostgreSql.Services.PostgresRuntimeConfigurationStore<RuntimeControls>>()))));
        using var client = host.CreateClient();
        var store = Assert.IsType<BlockingStore>(probe);
        await store.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<RuntimeControls>>();
        for (var index = 0; index < 1_000; index++)
            Assert.Equal(4, monitor.CurrentValue.Verbosity);
        await Task.Delay(350, TestContext.Current.CancellationToken);
        Assert.Equal(2, Volatile.Read(ref store.ReadCount));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.Services.GetRequiredService<IHost>().StopAsync(deadline.Token);
        Assert.True(await store.Cancelled.Task.WaitAsync(deadline.Token));
    }

    private sealed class BlockingStore(IRuntimeConfigurationStore<RuntimeControls> authority) : IRuntimeConfigurationStore<RuntimeControls>
    {
        public int ReadCount;
        public TaskCompletionSource<bool> Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<RuntimeConfigurationRevision<RuntimeControls>?> ReadAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref ReadCount) == 1)
                return await authority.ReadAsync(cancellationToken);
            Blocked.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult(true);
                throw;
            }
        }
    }

}
