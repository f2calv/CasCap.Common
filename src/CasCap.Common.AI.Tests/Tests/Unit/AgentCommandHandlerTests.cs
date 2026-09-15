using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Tests for <see cref="AgentCommandHandler"/> slash-command override state.
/// <para>
/// The handler is registered as a singleton and shared by every agent in the process, so the
/// central concern here is that an override applied to one agent never leaks into another.
/// </para>
/// </summary>
[Trait("Category", "Agent Commands")]
public class AgentCommandHandlerTests
{
    private const string AgentA = "comms-agent";
    private const string AgentB = "security-agent";

    /// <summary>Minimal in-memory <see cref="ISessionStore"/> recording whether it was consulted.</summary>
    private sealed class FakeSessionStore : ISessionStore
    {
        private readonly Dictionary<string, string> _store = [];

        public int GetCallCount { get; private set; }

        public ValueTask<string?> GetAsync(string key)
        {
            GetCallCount++;
            return new(_store.GetValueOrDefault(key));
        }

        public ValueTask SetAsync(string key, string json, TimeSpan? slidingExpiration = null)
        {
            _store[key] = json;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(string key)
        {
            _store.Remove(key);
            return ValueTask.CompletedTask;
        }
    }

    private static AIConfig NewAIConfig(string instructionsPrefix = "", string instructionsSuffix = "") => new()
    {
        Providers = new Dictionary<string, ProviderConfig>
        {
            ["test-provider"] = new() { Type = AgentType.Ollama, ModelName = "test-model", Endpoint = new Uri("http://ollama.local:11434") },
        },
        Agents = new Dictionary<string, AgentConfig>
        {
            [AgentA] = new() { Provider = "test-provider", Name = AgentA, Description = "d", Prompt = "p", Instructions = "i" },
        },
        InstructionsPrefix = instructionsPrefix,
        InstructionsSuffix = instructionsSuffix,
    };

    private static AgentCommandHandler NewHandler(FakeSessionStore store, AIConfig? config = null) =>
        new(NullLogger<AgentCommandHandler>.Instance, Options.Create(config ?? NewAIConfig()), store);

    /// <summary>A real agent is only needed so command dispatch has a non-null argument; no I/O occurs.</summary>
    private static AIAgent NewAgent()
    {
        var provider = new ProviderConfig { Type = AgentType.Ollama, ModelName = "test-model", Endpoint = new Uri("http://ollama.local:11434") };
        var agentConfig = new AgentConfig { Provider = "test-provider", Name = AgentA, Description = "d", Prompt = "p", Instructions = "i" };
        var (_, agent, _) = AgentExtensions.CreateAgent(provider, agentConfig);
        return agent;
    }

    [Fact]
    public void GetModelOverride_Default() =>
        Assert.Null(NewHandler(new FakeSessionStore()).GetModelOverride(AgentA));

    [Fact]
    public void GetInstructionsOverride_Default() =>
        Assert.Null(NewHandler(new FakeSessionStore()).GetInstructionsOverride(AgentA));

    [Fact]
    public void IsSessionEnabled_Default() =>
        Assert.True(NewHandler(new FakeSessionStore()).IsSessionEnabled(AgentA));

    /// <summary>A missing agent name must be rejected rather than silently sharing one bucket.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetModelOverride_InvalidAgentName(string? agentName) =>
        Assert.ThrowsAny<ArgumentException>(() => NewHandler(new FakeSessionStore()).GetModelOverride(agentName!));

    /// <summary>
    /// Regression test: <c>/model</c> previously wrote to a single field on the singleton, so
    /// overriding the model for one agent silently re-pointed every other agent too.
    /// </summary>
    [Fact]
    public async Task HandleCommandAsync_Model_IsolatedPerAgent()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();

        await handler.HandleCommandAsync(ChatCommand.Model, "qwen3:32b", agent, AgentA);

        Assert.Equal("qwen3:32b", handler.GetModelOverride(AgentA));
        Assert.Null(handler.GetModelOverride(AgentB));
    }

    /// <summary>Regression test: <c>/instructions</c> must not leak across agents either.</summary>
    [Fact]
    public async Task HandleCommandAsync_Instructions_IsolatedPerAgent()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();

        await handler.HandleCommandAsync(ChatCommand.Instructions, "be terse", agent, AgentA);

        Assert.Equal("be terse", handler.GetInstructionsOverride(AgentA));
        Assert.Null(handler.GetInstructionsOverride(AgentB));
    }

    /// <summary>Regression test: disabling session persistence must not disable it for every agent.</summary>
    [Fact]
    public async Task HandleCommandAsync_SessionDisable_IsolatedPerAgent()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();

        await handler.HandleCommandAsync(ChatCommand.SessionDisable, string.Empty, agent, AgentA);

        Assert.False(handler.IsSessionEnabled(AgentA));
        Assert.True(handler.IsSessionEnabled(AgentB));
    }

    [Fact]
    public async Task HandleCommandAsync_SessionEnable_RestoresPersistence()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();

        await handler.HandleCommandAsync(ChatCommand.SessionDisable, string.Empty, agent, AgentA);
        await handler.HandleCommandAsync(ChatCommand.SessionEnable, string.Empty, agent, AgentA);

        Assert.True(handler.IsSessionEnabled(AgentA));
    }

    /// <summary>Two agents may hold different overrides simultaneously.</summary>
    [Fact]
    public async Task HandleCommandAsync_Model_DistinctPerAgent()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();

        await handler.HandleCommandAsync(ChatCommand.Model, "qwen3:32b", agent, AgentA);
        await handler.HandleCommandAsync(ChatCommand.Model, "llama3:8b", agent, AgentB);

        Assert.Equal("qwen3:32b", handler.GetModelOverride(AgentA));
        Assert.Equal("llama3:8b", handler.GetModelOverride(AgentB));
    }

    [Fact]
    public async Task HandleCommandAsync_Model_NoArgumentReportsCurrent()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();

        await handler.HandleCommandAsync(ChatCommand.Model, "qwen3:32b", agent, AgentA);
        var response = await handler.HandleCommandAsync(ChatCommand.Model, string.Empty, agent, AgentA);

        Assert.Contains("qwen3:32b", response);
    }

    [Fact]
    public async Task ApplyModelOverride_IsolatedPerAgent()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();
        await handler.HandleCommandAsync(ChatCommand.Model, "qwen3:32b", agent, AgentA);

        var optionsA = new ChatOptions();
        var optionsB = new ChatOptions();
        handler.ApplyModelOverride(optionsA, AgentA);
        handler.ApplyModelOverride(optionsB, AgentB);

        Assert.Equal("qwen3:32b", optionsA.ModelId);
        Assert.Null(optionsB.ModelId);
    }

    [Fact]
    public async Task ApplyInstructionsOverride_IsolatedPerAgent()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();
        await handler.HandleCommandAsync(ChatCommand.Instructions, "be terse", agent, AgentA);

        var optionsA = new ChatOptions();
        var optionsB = new ChatOptions();
        handler.ApplyInstructionsOverride(optionsA, AgentA);
        handler.ApplyInstructionsOverride(optionsB, AgentB);

        Assert.Equal("be terse", optionsA.Instructions);
        Assert.Null(optionsB.Instructions);
    }

    /// <summary>The shared prefix/suffix must still wrap a per-agent instructions override.</summary>
    [Fact]
    public async Task ApplyInstructionsOverride_WrapsWithPrefixAndSuffix()
    {
        var config = NewAIConfig(instructionsPrefix: "PREFIX", instructionsSuffix: "SUFFIX");
        var handler = NewHandler(new FakeSessionStore(), config);
        var agent = NewAgent();
        await handler.HandleCommandAsync(ChatCommand.Instructions, "be terse", agent, AgentA);

        var options = new ChatOptions();
        handler.ApplyInstructionsOverride(options, AgentA, config);

        Assert.Equal("PREFIX be terse SUFFIX", options.Instructions);
    }

    /// <summary>A disabled session must short-circuit before the store is consulted.</summary>
    [Fact]
    public async Task LoadSessionAsync_SessionDisabled()
    {
        var store = new FakeSessionStore();
        var handler = NewHandler(store);
        var agent = NewAgent();
        await handler.HandleCommandAsync(ChatCommand.SessionDisable, string.Empty, agent, AgentA);

        var session = await handler.LoadSessionAsync(agent, AgentA);

        Assert.Null(session);
        Assert.Equal(0, store.GetCallCount);
    }

    /// <summary>Disabling one agent must not stop another agent loading its session.</summary>
    [Fact]
    public async Task LoadSessionAsync_OtherAgentUnaffected()
    {
        var store = new FakeSessionStore();
        var handler = NewHandler(store);
        var agent = NewAgent();
        await handler.HandleCommandAsync(ChatCommand.SessionDisable, string.Empty, agent, AgentA);

        var session = await handler.LoadSessionAsync(agent, AgentB);

        Assert.Null(session); // nothing stored, but the store was consulted
        Assert.Equal(1, store.GetCallCount);
    }

    /// <summary>Agent names are matched case-insensitively so config casing cannot split state.</summary>
    [Fact]
    public async Task GetModelOverride_AgentNameIsCaseInsensitive()
    {
        var handler = NewHandler(new FakeSessionStore());
        var agent = NewAgent();

        await handler.HandleCommandAsync(ChatCommand.Model, "qwen3:32b", agent, AgentA);

        Assert.Equal("qwen3:32b", handler.GetModelOverride(AgentA.ToUpperInvariant()));
    }
}
