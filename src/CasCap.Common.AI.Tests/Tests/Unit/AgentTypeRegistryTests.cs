namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Tests for <see cref="AgentTypeRegistry"/> and <c>AddAgentTypeRegistry</c>, which replaced the
/// load-order-dependent scan of every type in every loaded assembly.
/// </summary>
[Trait("Category", "Type Resolution")]
public class AgentTypeRegistryTests
{
    [McpServerToolType]
    public sealed class AlphaMcpQueryService
    {
        [McpServerTool, Description("does a thing")]
        public string DoThing() => "ok";
    }

    [McpServerToolType]
    public sealed class BetaMcpQueryService
    {
        [McpServerTool, Description("does another thing")]
        public string DoOtherThing() => "ok";
    }

    /// <summary>Registered in DI but exposes no tool methods, so must not be indexed.</summary>
    public sealed class NotAToolService
    {
        public string Unrelated() => "ok";
    }

    [McpServerPromptType]
    public static class AlphaPrompts
    {
        [McpServerPrompt, Description("a prompt")]
        public static string Summarise() => "summarise";
    }

    /// <summary>A second type sharing a simple name, emulating two assemblies colliding.</summary>
    private static class Nested
    {
        [McpServerToolType]
        public sealed class AlphaMcpQueryService
        {
            [McpServerTool, Description("a colliding thing")]
            public string DoThing() => "ok";
        }
    }

    [Fact]
    public void TryGetToolType_ResolvesRegisteredType()
    {
        var registry = new AgentTypeRegistry([typeof(AlphaMcpQueryService)], []);

        Assert.True(registry.TryGetToolType(nameof(AlphaMcpQueryService), out var type));
        Assert.Equal(typeof(AlphaMcpQueryService), type);
    }

    [Fact]
    public void TryGetToolType_UnknownName()
    {
        var registry = new AgentTypeRegistry([typeof(AlphaMcpQueryService)], []);

        Assert.False(registry.TryGetToolType("NoSuchService", out var type));
        Assert.Null(type);
    }

    [Fact]
    public void TryGetPromptType_ResolvesRegisteredType()
    {
        var registry = new AgentTypeRegistry([], [typeof(AlphaPrompts)]);

        Assert.True(registry.TryGetPromptType(nameof(AlphaPrompts), out var type));
        Assert.Equal(typeof(AlphaPrompts), type);
    }

    /// <summary>
    /// Two distinct types sharing a simple name previously resolved to whichever assembly happened
    /// to load first. The registry must reject the ambiguity at startup instead.
    /// </summary>
    [Fact]
    public void Constructor_AmbiguousToolName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new AgentTypeRegistry([typeof(AlphaMcpQueryService), typeof(Nested.AlphaMcpQueryService)], []));

        Assert.Contains(nameof(AlphaMcpQueryService), ex.Message);
        Assert.Contains("Ambiguous", ex.Message);
    }

    /// <summary>The same type listed twice is harmless and must not be treated as a collision.</summary>
    [Fact]
    public void Constructor_DuplicateRegistrationOfSameType()
    {
        var registry = new AgentTypeRegistry([typeof(AlphaMcpQueryService), typeof(AlphaMcpQueryService)], []);

        Assert.Single(registry.ToolTypeNames);
    }

    [Fact]
    public void ToolTypeNames_ListsEveryRegisteredName()
    {
        var registry = new AgentTypeRegistry([typeof(AlphaMcpQueryService), typeof(BetaMcpQueryService)], []);

        Assert.Equal(
            [nameof(AlphaMcpQueryService), nameof(BetaMcpQueryService)],
            registry.ToolTypeNames.Order());
    }

    [Fact]
    public void AddAgentTypeRegistry_IndexesRegisteredToolServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AlphaMcpQueryService>();
        services.AddSingleton<BetaMcpQueryService>();

        var registry = services.AddAgentTypeRegistry()
            .BuildServiceProvider()
            .GetRequiredService<AgentTypeRegistry>();

        Assert.True(registry.TryGetToolType(nameof(AlphaMcpQueryService), out _));
        Assert.True(registry.TryGetToolType(nameof(BetaMcpQueryService), out _));
    }

    /// <summary>Only types exposing tool methods belong in the index.</summary>
    [Fact]
    public void AddAgentTypeRegistry_IgnoresServicesWithoutToolMethods()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AlphaMcpQueryService>();
        services.AddSingleton<NotAToolService>();

        var registry = services.AddAgentTypeRegistry()
            .BuildServiceProvider()
            .GetRequiredService<AgentTypeRegistry>();

        Assert.False(registry.TryGetToolType(nameof(NotAToolService), out _));
        Assert.Single(registry.ToolTypeNames);
    }

    /// <summary>Prompt types are static and absent from DI, so they come from supplied assemblies.</summary>
    [Fact]
    public void AddAgentTypeRegistry_IndexesPromptTypesFromAssembly()
    {
        var registry = new ServiceCollection()
            .AddAgentTypeRegistry(typeof(AgentTypeRegistryTests).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<AgentTypeRegistry>();

        Assert.True(registry.TryGetPromptType(nameof(AlphaPrompts), out var type));
        Assert.Equal(typeof(AlphaPrompts), type);
    }

    /// <summary>
    /// A misconfigured tool name must fail with a message naming the known alternatives, rather
    /// than the previous bare "not found in any loaded assembly".
    /// </summary>
    [Fact]
    public void CreateToolsForAgent_UnknownToolServiceListsKnownNames()
    {
        var provider = new ServiceCollection()
            .AddSingleton<AlphaMcpQueryService>()
            .AddAgentTypeRegistry()
            .BuildServiceProvider();

        var agentConfig = new AgentConfig
        {
            Provider = "p",
            Name = "a",
            Description = "d",
            Prompt = "p",
            Instructions = "i",
            Tools = [new ToolSource { Service = "TypoedMcpQueryService" }],
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AgentExtensions.CreateToolsForAgent(provider, agentConfig));

        Assert.Contains("TypoedMcpQueryService", ex.Message);
        Assert.Contains(nameof(AlphaMcpQueryService), ex.Message);
    }

    /// <summary>The registry path resolves a correctly configured tool source end to end.</summary>
    [Fact]
    public void CreateToolsForAgent_ResolvesViaRegistry()
    {
        var provider = new ServiceCollection()
            .AddSingleton<AlphaMcpQueryService>()
            .AddAgentTypeRegistry()
            .BuildServiceProvider();

        var agentConfig = new AgentConfig
        {
            Provider = "p",
            Name = "a",
            Description = "d",
            Prompt = "p",
            Instructions = "i",
            Tools = [new ToolSource { Service = nameof(AlphaMcpQueryService) }],
        };

        var tools = AgentExtensions.CreateToolsForAgent(provider, agentConfig);

        Assert.Equal("do_thing", Assert.Single(tools).Name);
    }
}
