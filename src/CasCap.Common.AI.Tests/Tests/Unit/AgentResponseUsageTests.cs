namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Verifies that the agent framework aggregates <see cref="UsageDetails"/> across the multiple
/// <see cref="IChatClient.GetResponseAsync"/> round-trips of a tool-calling loop and surfaces the
/// total on <see cref="AgentResponse.Usage"/>.
/// <para>
/// This behaviour is what allows <c>AgentExtensions</c> to report token usage without the
/// hand-rolled ambient accumulator it previously carried, so it is pinned by a test rather than
/// assumed.
/// </para>
/// </summary>
[Trait("Category", "Usage Reporting")]
public class AgentResponseUsageTests
{
    /// <summary>An <see cref="IChatClient"/> that replays a fixed script of responses.</summary>
    private sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
    {
        private int _index;

        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(responses[Math.Min(_index++, responses.Length - 1)]);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static ChatResponse ToolCallResponse(string callId, string toolName, long inputTokens, long outputTokens) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(callId, toolName, new Dictionary<string, object?>())]))
        {
            Usage = new UsageDetails { InputTokenCount = inputTokens, OutputTokenCount = outputTokens, TotalTokenCount = inputTokens + outputTokens },
        };

    private static ChatResponse TextResponse(string text, long inputTokens, long outputTokens) =>
        new(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails { InputTokenCount = inputTokens, OutputTokenCount = outputTokens, TotalTokenCount = inputTokens + outputTokens },
        };

    private static (AIAgent Agent, ScriptedChatClient Script) BuildAgent(params ChatResponse[] responses)
    {
        var script = new ScriptedChatClient(responses);
        var chatClient = script.AsBuilder().UseFunctionInvocation().Build();

        var agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "usage-probe",
            ChatOptions = new ChatOptions
            {
                Tools = [AIFunctionFactory.Create(() => "ok", "test_tool")],
            },
        });

        return (agent, script);
    }

    /// <summary>A single round-trip surfaces that call's usage.</summary>
    [Fact]
    public async Task Usage_SingleRoundTrip()
    {
        var (agent, script) = BuildAgent(TextResponse("hello", inputTokens: 11, outputTokens: 3));

        var response = await agent.RunAsync("hi", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, script.CallCount);
        Assert.NotNull(response.Usage);
        Assert.Equal(11, response.Usage.InputTokenCount);
        Assert.Equal(3, response.Usage.OutputTokenCount);
    }

    /// <summary>
    /// The decisive case: a tool call forces a second <see cref="IChatClient.GetResponseAsync"/>
    /// round-trip, and <see cref="AgentResponse.Usage"/> must report the sum of both.
    /// </summary>
    [Fact]
    public async Task Usage_AggregatedAcrossToolCallRoundTrips()
    {
        var (agent, script) = BuildAgent(
            ToolCallResponse("call-1", "test_tool", inputTokens: 10, outputTokens: 5),
            TextResponse("done", inputTokens: 20, outputTokens: 7));

        var response = await agent.RunAsync("hi", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, script.CallCount);
        Assert.NotNull(response.Usage);
        Assert.Equal(30, response.Usage.InputTokenCount);
        Assert.Equal(12, response.Usage.OutputTokenCount);
        Assert.Equal(42, response.Usage.TotalTokenCount);
    }

    private static ProviderConfig NewProviderConfig() =>
        new() { Type = AgentType.Ollama, ModelName = "test-model", Endpoint = new Uri("http://ollama.local:11434") };

    private static AgentConfig NewAgentConfig() =>
        new() { Provider = "test-provider", Name = "usage-probe", Description = "d", Prompt = "p", Instructions = "i" };

    private static Task<AgentRunResult> RunAnalysisAsync(AIAgent agent) =>
        agent.RunAnalysisAsync(
            NewProviderConfig(),
            NewAgentConfig(),
            AgentExtensions.BuildChatMessage("hi"),
            new ChatOptions(),
            cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>
    /// <see cref="AgentExtensions.RunAnalysisAsync"/> must surface the framework's aggregated
    /// usage on <see cref="AgentRunResult.Usage"/>, which is what replaced the hand-rolled
    /// ambient accumulator.
    /// </summary>
    [Fact]
    public async Task RunAnalysisAsync_ReportsAggregatedUsage()
    {
        var (agent, _) = BuildAgent(
            ToolCallResponse("call-1", "test_tool", inputTokens: 10, outputTokens: 5),
            TextResponse("done", inputTokens: 20, outputTokens: 7));

        var result = await RunAnalysisAsync(agent);

        Assert.NotNull(result.Usage);
        Assert.Equal(30, result.Usage.InputTokenCount);
        Assert.Equal(12, result.Usage.OutputTokenCount);
    }

    /// <summary>A provider that reports no usage must leave <see cref="AgentRunResult.Usage"/> null rather than a hollow instance.</summary>
    [Fact]
    public async Task RunAnalysisAsync_WithoutUsage()
    {
        var (agent, _) = BuildAgent(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));

        var result = await RunAnalysisAsync(agent);

        Assert.Null(result.Usage);
    }

    /// <summary>Tool-call accounting must survive the removal of the usage accumulator.</summary>
    [Fact]
    public async Task RunAnalysisAsync_CountsToolCalls()
    {
        var (agent, _) = BuildAgent(
            ToolCallResponse("call-1", "test_tool", inputTokens: 10, outputTokens: 5),
            TextResponse("done", inputTokens: 20, outputTokens: 7));

        var result = await RunAnalysisAsync(agent);

        Assert.Equal(1, result.ToolCallCount);
        Assert.Equal("test_tool", Assert.Single(result.ToolCalls).Name);
        Assert.Equal("done", result.OutputText);
    }
}
