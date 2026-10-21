namespace CasCap.Common.Tests;

/// <summary>Tests for <see cref="SinkServiceCollectionExtensions.AddEventSinks{TEvent}(IServiceCollection, SinkConfig, System.Reflection.Assembly[])"/>.</summary>
[Trait("Category", "Sinks")]
public sealed class SinkServiceCollectionExtensionsTests
{
    /// <summary>Every enabled sink writes, while the preferred sink answers queries.</summary>
    [Fact]
    public void AddEventSinks_KeepsEveryWriterAndQueriesThePreferredSink()
    {
        using var provider = Register(Config("AzureTables", "Redis"));

        var writers = provider.GetServices<IEventSink<TestEvent>>().Select(s => s.GetType()).ToList();
        Assert.Contains(typeof(RedisTestSink), writers);
        Assert.Contains(typeof(TableTestSink), writers);
        Assert.IsType<RedisTestSink>(provider.GetRequiredService<ITestQuery>());
        Assert.IsType<RedisTestSink>(provider.GetRequiredKeyedService<IEventSink<TestEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
    }

    /// <summary>Without the preferred sink, the last registered query-capable sink answers queries.</summary>
    [Fact]
    public void AddEventSinks_FallsBackWhenThePreferredSinkIsDisabled()
    {
        using var provider = Register(Config("AzureTables"));

        Assert.IsType<TableTestSink>(Assert.Single(provider.GetServices<IEventSink<TestEvent>>()));
        Assert.IsType<TableTestSink>(provider.GetRequiredService<ITestQuery>());
    }

    /// <summary>A query-capable fallback registered by an earlier call stops writing once real sinks register.</summary>
    [Fact]
    public void AddEventSinks_ReplacesAnEarlierFallbackQuerySink()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEventSink<TestEvent>, MemoryFallbackSink>(SinkServiceCollectionExtensions.PrimarySinkKey);

        using var provider = Register(Config("AzureTables", "Redis"), services);

        var writers = provider.GetServices<IEventSink<TestEvent>>().Select(s => s.GetType()).ToList();
        Assert.DoesNotContain(typeof(MemoryFallbackSink), writers);
        Assert.IsType<RedisTestSink>(provider.GetRequiredService<ITestQuery>());
    }

    private static SinkConfig Config(params string[] enabled) => new()
    {
        AvailableSinks = enabled.ToDictionary(s => s, _ => new SinkConfigParams { Enabled = true }),
    };

    private static ServiceProvider Register(SinkConfig config, ServiceCollection? services = null)
    {
        services ??= new ServiceCollection();
        services.AddEventSinks<TestEvent>(config, typeof(SinkServiceCollectionExtensionsTests).Assembly);
        return services.BuildServiceProvider();
    }

    public sealed record TestEvent;

    public interface ITestQuery;

    [SinkType("AzureTables")]
    public sealed class TableTestSink : IEventSink<TestEvent>, ITestQuery
    {
        public string SinkType => "AzureTables";
        public Task WriteEvent(TestEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [SinkType("Redis")]
    public sealed class RedisTestSink : IEventSink<TestEvent>, ITestQuery
    {
        public string SinkType => "Redis";
        public Task WriteEvent(TestEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed class MemoryFallbackSink : IEventSink<TestEvent>, ITestQuery
    {
        public string SinkType => "Memory";
        public Task WriteEvent(TestEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
